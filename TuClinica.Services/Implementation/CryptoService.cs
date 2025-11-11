// En: TuClinica.Services/Implementation/CryptoService.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Services;
using System.Threading; // <-- AÑADIDO PARA CancellationToken

namespace TuClinica.Services.Implementation
{
    public class CryptoService : ICryptoService
    {
        // Constantes para la nueva implementación de streaming (CBC+HMAC)
        private const int SaltSize = 16;
        private const int IvSize = 16; // AES usa un bloque de 128 bits (16 bytes)
        private const int HmacSize = 32; // HMAC-SHA256 produce un hash de 32 bytes
        private const int KeySize = 32; // AES-256
        private const int Iterations = 10000; // Mantenemos las mismas iteraciones
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

        #region Métodos AES-GCM (Originales, para datos en memoria)

        public byte[] Encrypt(byte[] dataToEncrypt, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, _hashAlgorithm);
            byte[] key = keyDerivation.GetBytes(KeySize); // AES-256
            byte[] nonce = RandomNumberGenerator.GetBytes(12); // AES-GCM nonce
            using var aesGcm = new AesGcm(key);
            byte[] cipherText = new byte[dataToEncrypt.Length];
            byte[] tag = new byte[16]; // GCM Auth Tag
            aesGcm.Encrypt(nonce, dataToEncrypt, cipherText, tag);

            // Combine: [salt][nonce][tag][ciphertext]
            byte[] encryptedDataWithMeta = new byte[salt.Length + nonce.Length + tag.Length + cipherText.Length];
            Buffer.BlockCopy(salt, 0, encryptedDataWithMeta, 0, salt.Length);
            Buffer.BlockCopy(nonce, 0, encryptedDataWithMeta, salt.Length, nonce.Length);
            Buffer.BlockCopy(tag, 0, encryptedDataWithMeta, salt.Length + nonce.Length, tag.Length);
            Buffer.BlockCopy(cipherText, 0, encryptedDataWithMeta, salt.Length + nonce.Length + tag.Length, cipherText.Length);
            return encryptedDataWithMeta;
        }

        public byte[]? Decrypt(byte[] encryptedDataWithMeta, string password)
        {
            const int gcmSaltSize = 16; const int gcmNonceSize = 12; const int gcmTagSize = 16;
            int expectedMinLength = gcmSaltSize + gcmNonceSize + gcmTagSize;

            if (encryptedDataWithMeta == null || encryptedDataWithMeta.Length < expectedMinLength)
            {
                return null;
            }

            try
            {
                // Extract metadata
                byte[] salt = new byte[gcmSaltSize]; byte[] nonce = new byte[gcmNonceSize]; byte[] tag = new byte[gcmTagSize];
                int cipherTextLength = encryptedDataWithMeta.Length - expectedMinLength;
                byte[] cipherText = new byte[cipherTextLength];
                Buffer.BlockCopy(encryptedDataWithMeta, 0, salt, 0, gcmSaltSize);
                Buffer.BlockCopy(encryptedDataWithMeta, gcmSaltSize, nonce, 0, gcmNonceSize);
                Buffer.BlockCopy(encryptedDataWithMeta, gcmSaltSize + gcmNonceSize, tag, 0, gcmTagSize);
                Buffer.BlockCopy(encryptedDataWithMeta, expectedMinLength, cipherText, 0, cipherTextLength);

                // Derive key
                using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, _hashAlgorithm);
                byte[] key = keyDerivation.GetBytes(KeySize);

                // Decrypt
                using var aesGcm = new AesGcm(key);
                byte[] decryptedData = new byte[cipherText.Length];
                aesGcm.Decrypt(nonce, cipherText, tag, decryptedData);
                return decryptedData;
            }
            catch (CryptographicException) // Ocurre si el 'tag' (contraseña) es incorrecto
            {
                return null;
            }
            catch (Exception)
            {
                return null; // Return null on unexpected errors too
            }
        }

        #endregion

        #region Métodos de Streaming (Nuevos, AES-CBC + HMAC)

        /// <summary>
        /// Deriva dos claves (una para cifrado, otra para HMAC) desde una contraseña y salt.
        /// </summary>
        private (byte[] aesKey, byte[] hmacKey) DeriveKeys(string password, byte[] salt)
        {
            // Derivamos una clave combinada de 64 bytes
            using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, _hashAlgorithm);
            byte[] combinedKey = keyDerivation.GetBytes(KeySize + HmacSize); // 32 + 32 = 64 bytes

            // Dividimos la clave
            byte[] aesKey = combinedKey[..KeySize]; // Primeros 32 bytes para AES
            byte[] hmacKey = combinedKey[KeySize..]; // Siguientes 32 bytes para HMAC

            return (aesKey, hmacKey);
        }

        /// <summary>
        /// Encripta un stream (JSON) a otro stream (archivo .bak)
        /// Formato de archivo: [Salt (16)] [IV (16)] [Ciphertext (...)] [HMAC (32)]
        /// </summary>
        public async Task EncryptAsync(Stream inputStream, Stream outputStream, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            (byte[] aesKey, byte[] hmacKey) = DeriveKeys(password, salt);

            using Aes aes = Aes.Create();
            aes.Key = aesKey;
            aes.Mode = CipherMode.CBC; // CBC es compatible con CryptoStream
            aes.Padding = PaddingMode.PKCS7;
            byte[] iv = aes.IV; // Genera un IV aleatorio (16 bytes)

            // Escribir metadatos no encriptados al inicio del archivo
            await outputStream.WriteAsync(salt, 0, SaltSize);
            await outputStream.WriteAsync(iv, 0, IvSize);

            // Usamos dos streams anidados para aplicar Encrypt-then-MAC

            using HMACSHA256 hmac = new HMACSHA256(hmacKey);

            // --- INICIO DE LA CORRECCIÓN ---

            // 1. Creamos el hashingStream, asegurando que NO cierre outputStream
            await using (var hashingStream = new CryptoStream(outputStream, hmac, CryptoStreamMode.Write, leaveOpen: true))
            {
                // 2. Creamos el encryptStream, asegurando que NO cierre hashingStream
                await using (var encryptStream = new CryptoStream(hashingStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
                {
                    // 3. Encryptamos los datos.
                    // Flujo: input -> encryptStream -> hashingStream -> outputStream
                    await inputStream.CopyToAsync(encryptStream);

                } // 'encryptStream' se cierra, llama a FlushFinalBlock() UNA VEZ, y deja 'hashingStream' abierto.

            } // 'hashingStream' se cierra, llama a FlushFinalBlock() UNA VEZ, y deja 'outputStream' abierto.

            // 5. AHORA que ambos streams han flusheado y están cerrados, el hash está finalizado.
            byte[] hash = hmac.Hash ?? throw new CryptographicException("Error al calcular el HMAC.");

            // 6. Escribimos el hash al final del outputStream (que sigue abierto).
            await outputStream.WriteAsync(hash, 0, hash.Length);

            // --- FIN DE LA CORRECCIÓN ---
        }


        /// <summary>
        /// Desencripta un stream (archivo .bak) a otro stream (JSON)
        /// Formato de archivo: [Salt (16)] [IV (16)] [Ciphertext (...)] [HMAC (32)]
        /// </summary>
        public async Task DecryptAsync(Stream inputStream, Stream outputStream, string password)
        {
            // Leer los metadatos no encriptados
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];

            if (await inputStream.ReadAsync(salt, 0, SaltSize) < SaltSize)
                throw new CryptographicException("Archivo corrupto (salt).");

            if (await inputStream.ReadAsync(iv, 0, IvSize) < IvSize)
                throw new CryptographicException("Archivo corrupto (iv).");

            // Derivar claves (igual que en Encrypt)
            (byte[] aesKey, byte[] hmacKey) = DeriveKeys(password, salt);

            // Leer el HMAC *primero* (está al final del archivo)
            byte[] storedHmac = new byte[HmacSize];
            long ciphertextLength = inputStream.Length - SaltSize - IvSize - HmacSize;
            if (ciphertextLength < 0)
                throw new CryptographicException("Archivo corrupto (longitud).");

            // Posicionamos el stream al inicio del HMAC (Salt + IV + Ciphertext)
            inputStream.Seek(SaltSize + IvSize + ciphertextLength, SeekOrigin.Begin);
            if (await inputStream.ReadAsync(storedHmac, 0, HmacSize) < HmacSize)
                throw new CryptographicException("Archivo corrupto (hmac).");

            // Rebobinamos al inicio del ciphertext
            inputStream.Seek(SaltSize + IvSize, SeekOrigin.Begin);

            // Verificamos el HMAC del ciphertext
            using HMACSHA256 hmac = new HMACSHA256(hmacKey);

            // Creamos un stream limitado que solo lee el ciphertext
            var limitedStream = new LimitedStream(inputStream, ciphertextLength);
            byte[] computedHmac = await hmac.ComputeHashAsync(limitedStream);

            // Comparamos los hashes. ¡Es crucial usar FixedTimeEquals!
            if (!CryptographicOperations.FixedTimeEquals(storedHmac, computedHmac))
            {
                // ¡Contraseña incorrecta O archivo manipulado!
                throw new CryptographicException("Error de autenticación: contraseña incorrecta o datos corruptos.");
            }

            // Si el HMAC es válido, procedemos a desencriptar
            // Rebobinamos al inicio del ciphertext
            inputStream.Seek(SaltSize + IvSize, SeekOrigin.Begin);

            using Aes aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Creamos un stream limitado para el desencriptador, para que no lea el HMAC del final
            var decryptLimitedStream = new LimitedStream(inputStream, ciphertextLength);

            await using var decryptStream = new CryptoStream(decryptLimitedStream, aes.CreateDecryptor(), CryptoStreamMode.Read);

            // Copia los datos desencriptados (JSON) al stream de salida
            await decryptStream.CopyToAsync(outputStream);
        }

        #endregion

        // Clase auxiliar para leer solo una porción de un stream (para el HMAC)
        // --- CORREGIDO: Se ha hecho más robusto para lecturas múltiples ---
        private class LimitedStream : Stream
        {
            private readonly Stream _baseStream;
            private long _remaining;
            private readonly long _initialPosition;

            public LimitedStream(Stream baseStream, long length)
            {
                if (!baseStream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(baseStream));
                if (!baseStream.CanSeek) throw new ArgumentException("Stream must be seekable for this implementation.", nameof(baseStream));

                _baseStream = baseStream;
                _remaining = length;
                _initialPosition = _baseStream.Position; // Guardar la posición inicial
            }

            public override bool CanRead => true;
            public override bool CanSeek => _baseStream.CanSeek;
            public override bool CanWrite => false;
            public override long Length => _remaining;

            public override long Position
            {
                get => _baseStream.Position - _initialPosition;
                set => _baseStream.Position = _initialPosition + value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining == 0) return 0;
                int countToRead = (int)Math.Min(count, _remaining);
                int read = _baseStream.Read(buffer, offset, countToRead);
                _remaining -= read;
                return read;
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                if (_remaining == 0) return 0;
                int countToRead = (int)Math.Min(count, (int)_remaining); // Cuidado con 'int'
                // --- CORRECCIÓN: Usar AsMemory para compatibilidad con ReadAsync ---
                int read = await _baseStream.ReadAsync(buffer.AsMemory(offset, countToRead), cancellationToken);
                _remaining -= read;
                return read;
            }

            public override void Flush() => _baseStream.Flush();

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPos = offset;
                        break;
                    case SeekOrigin.Current:
                        newPos = Position + offset;
                        break;
                    case SeekOrigin.End:
                        newPos = Length + offset;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin));
                }

                if (newPos < 0) throw new IOException("Cannot seek before beginning of stream.");
                if (newPos > Length) newPos = Length;

                // --- CORRECCIÓN de lógica de Seek/Posicionamiento ---
                long oldPos = Position;
                long newBasePos = _initialPosition + newPos;
                _remaining -= (newPos - oldPos); // Ajustar 'remaining' basado en el cambio de posición

                return _baseStream.Seek(newBasePos, SeekOrigin.Begin) - _initialPosition;
            }

            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            // Importante: No cerrar el stream base
            protected override void Dispose(bool disposing)
            {
                // No hacemos nada para no cerrar el _baseStream
            }

            // --- AÑADIDO: Implementar DisposeAsync para 'await using' ---
            public override async ValueTask DisposeAsync()
            {
                // No hacemos nada para no cerrar el _baseStream
                await Task.CompletedTask;
            }
        }
    }
}