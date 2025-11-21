using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using TuClinica.Core.Interfaces.Services;
using System.Threading;

namespace TuClinica.Services.Implementation
{
    public class CryptoService : ICryptoService
    {
        // --- Constantes de Configuración ---
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const int KeySize = 32; // AES-256
        private const int Iterations = 10000;
        private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

        #region Métodos AES-GCM (En Memoria - Sin cambios)

        public byte[] Encrypt(byte[] dataToEncrypt, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, _hashAlgorithm);
            byte[] key = keyDerivation.GetBytes(KeySize);
            byte[] nonce = RandomNumberGenerator.GetBytes(12);
            using var aesGcm = new AesGcm(key);
            byte[] cipherText = new byte[dataToEncrypt.Length];
            byte[] tag = new byte[16];
            aesGcm.Encrypt(nonce, dataToEncrypt, cipherText, tag);

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
                return null;

            try
            {
                byte[] salt = new byte[gcmSaltSize]; byte[] nonce = new byte[gcmNonceSize]; byte[] tag = new byte[gcmTagSize];
                int cipherTextLength = encryptedDataWithMeta.Length - expectedMinLength;
                byte[] cipherText = new byte[cipherTextLength];
                Buffer.BlockCopy(encryptedDataWithMeta, 0, salt, 0, gcmSaltSize);
                Buffer.BlockCopy(encryptedDataWithMeta, gcmSaltSize, nonce, 0, gcmNonceSize);
                Buffer.BlockCopy(encryptedDataWithMeta, gcmSaltSize + gcmNonceSize, tag, 0, gcmTagSize);
                Buffer.BlockCopy(encryptedDataWithMeta, expectedMinLength, cipherText, 0, cipherTextLength);

                using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, _hashAlgorithm);
                byte[] key = keyDerivation.GetBytes(KeySize);

                using var aesGcm = new AesGcm(key);
                byte[] decryptedData = new byte[cipherText.Length];
                aesGcm.Decrypt(nonce, cipherText, tag, decryptedData);
                return decryptedData;
            }
            catch (Exception) { return null; }
        }

        #endregion

        #region Métodos de Streaming (AES-CBC + HMAC + FALLBACK)

        private (byte[] aesKey, byte[] hmacKey) DeriveKeys(string password, byte[] salt)
        {
            using var keyDerivation = new Rfc2898DeriveBytes(password, salt, Iterations, _hashAlgorithm);
            byte[] combinedKey = keyDerivation.GetBytes(KeySize + HmacSize);
            byte[] aesKey = combinedKey[..KeySize];
            byte[] hmacKey = combinedKey[KeySize..];
            return (aesKey, hmacKey);
        }

        public async Task EncryptAsync(Stream inputStream, Stream outputStream, string password)
        {
            // Este método siempre usa el formato NUEVO (El más seguro)
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            (byte[] aesKey, byte[] hmacKey) = DeriveKeys(password, salt);

            using Aes aes = Aes.Create();
            aes.Key = aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            byte[] iv = aes.IV;

            await outputStream.WriteAsync(salt, 0, SaltSize);
            await outputStream.WriteAsync(iv, 0, IvSize);

            using HMACSHA256 hmac = new HMACSHA256(hmacKey);
            await using (var hashingStream = new CryptoStream(outputStream, hmac, CryptoStreamMode.Write, leaveOpen: true))
            {
                await using (var encryptStream = new CryptoStream(hashingStream, aes.CreateEncryptor(), CryptoStreamMode.Write, leaveOpen: true))
                {
                    await inputStream.CopyToAsync(encryptStream);
                }
            }

            byte[] hash = hmac.Hash ?? throw new CryptographicException("Error al calcular el HMAC.");
            await outputStream.WriteAsync(hash, 0, hash.Length);
        }

        /// <summary>
        /// Intenta desencriptar usando el método moderno (HMAC). Si falla, intenta el método Legacy.
        /// </summary>
        public async Task DecryptAsync(Stream inputStream, Stream outputStream, string password)
        {
            if (!inputStream.CanSeek)
                throw new ArgumentException("El stream de entrada debe ser 'Seekable' para soportar la detección de formato.");

            long startPosition = inputStream.Position;

            try
            {
                // INTENTO 1: Formato Nuevo (AES-CBC + HMAC)
                await DecryptWithHmacAsync(inputStream, outputStream, password);
            }
            catch (Exception)
            {
                // Si falla (HMAC inválido, clave incorrecta para este formato, etc.)
                // REBOBINAMOS e intentamos el formato antiguo.

                inputStream.Seek(startPosition, SeekOrigin.Begin);

                // Si outputStream es un MemoryStream o FileStream, intentamos limpiar lo que se haya escrito parcialmente
                if (outputStream.CanSeek)
                {
                    outputStream.SetLength(0);
                }

                // INTENTO 2: Formato Legacy (AES-CBC simple o tu método anterior)
                try
                {
                    await DecryptLegacyAsync(inputStream, outputStream, password);
                }
                catch (CryptographicException ex)
                {
                    // Capturamos el error de "Padding invalid" aquí para que no cierre la app abruptamente
                    // Puedes loguear el error si tienes logger
                    throw new Exception("La contraseña es incorrecta o el formato de la base de datos no es compatible.", ex);
                }
            }
        }

        // --- Lógica del Formato Nuevo (Tu código original de AES+HMAC) ---
        private async Task DecryptWithHmacAsync(Stream inputStream, Stream outputStream, string password)
        {
            byte[] salt = new byte[SaltSize];
            byte[] iv = new byte[IvSize];

            if (await inputStream.ReadAsync(salt, 0, SaltSize) < SaltSize) throw new CryptographicException("Archivo corrupto (salt).");
            if (await inputStream.ReadAsync(iv, 0, IvSize) < IvSize) throw new CryptographicException("Archivo corrupto (iv).");

            (byte[] aesKey, byte[] hmacKey) = DeriveKeys(password, salt);

            byte[] storedHmac = new byte[HmacSize];
            long ciphertextLength = inputStream.Length - SaltSize - IvSize - HmacSize;
            if (ciphertextLength < 0) throw new CryptographicException("Archivo corrupto (longitud).");

            inputStream.Seek(SaltSize + IvSize + ciphertextLength, SeekOrigin.Begin);
            if (await inputStream.ReadAsync(storedHmac, 0, HmacSize) < HmacSize) throw new CryptographicException("Archivo corrupto (hmac).");

            inputStream.Seek(SaltSize + IvSize, SeekOrigin.Begin);

            using HMACSHA256 hmac = new HMACSHA256(hmacKey);
            var limitedStream = new LimitedStream(inputStream, ciphertextLength);
            byte[] computedHmac = await hmac.ComputeHashAsync(limitedStream);

            if (!CryptographicOperations.FixedTimeEquals(storedHmac, computedHmac))
            {
                throw new CryptographicException("Fallo de integridad HMAC (Formato incorrecto o contraseña errónea).");
            }

            inputStream.Seek(SaltSize + IvSize, SeekOrigin.Begin);

            using Aes aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var decryptLimitedStream = new LimitedStream(inputStream, ciphertextLength);
            await using var decryptStream = new CryptoStream(decryptLimitedStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
            await decryptStream.CopyToAsync(outputStream);
        }

        // --- Lógica del Formato Antiguo (Legacy) ---
        // IMPORTANTE: Ajusta esto si tu método anterior era diferente (ej. AES-GCM, sin Salt, etc.)
        // Esta es una implementación estándar de AES-CBC con Salt+IV al inicio pero SIN HMAC al final.
        // --- REEMPLAZA TODO EL MÉTODO DecryptLegacyAsync CON ESTE BLOQUE MEJORADO ---

        private async Task DecryptLegacyAsync(Stream inputStream, Stream outputStream, string password)
        {
            long startPosition = inputStream.Position;
            Exception lastError = null;

            // --- ESTRATEGIA 1: IMPLEMENTACIÓN "SIMPLE" (Muy común en tutoriales) ---
            // Lógica: La clave es simplemente el SHA256 del password. El archivo empieza con el IV (16 bytes).
            // Estructura archivo: [IV (16 bytes)] [Cifrado...]
            try
            {
                inputStream.Seek(startPosition, SeekOrigin.Begin);
                if (outputStream.CanSeek) outputStream.SetLength(0);

                byte[] iv = new byte[IvSize]; // 16 bytes
                if (await inputStream.ReadAsync(iv, 0, IvSize) == IvSize)
                {
                    using var sha256 = SHA256.Create();
                    byte[] key = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));

                    using Aes aes = Aes.Create();
                    aes.Key = key;
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using var decryptor = aes.CreateDecryptor();
                    await using var decryptStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
                    await decryptStream.CopyToAsync(outputStream);
                    return; // ¡Éxito!
                }
            }
            catch (Exception ex) { lastError = ex; }


            // --- ESTRATEGIA 2: ESTÁNDAR CON SALT (Lo que probamos antes) ---
            // Estructura: [Salt (16 bytes)] [IV (16 bytes)] [Cifrado...]
            var configs = new[]
            {
        new { Iterations = 1000, Algo = HashAlgorithmName.SHA1 },    // .NET Framework clásico
        new { Iterations = 10000, Algo = HashAlgorithmName.SHA256 }, // Estándar moderno
        new { Iterations = 2, Algo = HashAlgorithmName.SHA1 }        // Versiones muy viejas
    };

            byte[] salt = new byte[SaltSize];
            byte[] iv2 = new byte[IvSize];

            // Intentamos leer 32 bytes (Salt + IV)
            inputStream.Seek(startPosition, SeekOrigin.Begin);
            bool headerRead = false;
            if (await inputStream.ReadAsync(salt, 0, SaltSize) == SaltSize &&
                await inputStream.ReadAsync(iv2, 0, IvSize) == IvSize)
            {
                headerRead = true;
            }

            if (headerRead)
            {
                foreach (var config in configs)
                {
                    try
                    {
                        // Importante: Volver justo después del Salt+IV
                        inputStream.Seek(startPosition + SaltSize + IvSize, SeekOrigin.Begin);
                        if (outputStream.CanSeek) outputStream.SetLength(0);

                        using var keyDerivation = new Rfc2898DeriveBytes(password, salt, config.Iterations, config.Algo);
                        byte[] key = keyDerivation.GetBytes(KeySize);

                        using Aes aes = Aes.Create();
                        aes.Key = key;
                        aes.IV = iv2;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        using var decryptor = aes.CreateDecryptor();
                        await using var decryptStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
                        await decryptStream.CopyToAsync(outputStream);
                        return; // ¡Éxito!
                    }
                    catch (Exception ex) { lastError = ex; }
                }
            }

            // --- ESTRATEGIA 3: SIN IV EN ARCHIVO (Poco común pero posible) ---
            // Algunos sistemas antiguos usaban un IV fijo (todo ceros) y solo guardaban el cifrado.
            try
            {
                inputStream.Seek(startPosition, SeekOrigin.Begin);
                if (outputStream.CanSeek) outputStream.SetLength(0);

                using var sha256 = SHA256.Create();
                byte[] key = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                byte[] zeroIv = new byte[16]; // Todo ceros

                using Aes aes = Aes.Create();
                aes.Key = key;
                aes.IV = zeroIv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var decryptor = aes.CreateDecryptor();
                await using var decryptStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);
                await decryptStream.CopyToAsync(outputStream);
                return;
            }
            catch (Exception ex) { lastError = ex; }

            throw new Exception("No se pudo descifrar con ningún método conocido. Verifique la contraseña.", lastError);
        }

        #endregion

        // Helper para leer segmentos de stream sin cerrarlo
        private class LimitedStream : Stream
        {
            private readonly Stream _baseStream;
            private long _remaining;
            private readonly long _initialPosition;

            public LimitedStream(Stream baseStream, long length)
            {
                if (!baseStream.CanRead) throw new ArgumentException("Stream must be readable.");
                if (!baseStream.CanSeek) throw new ArgumentException("Stream must be seekable.");
                _baseStream = baseStream;
                _remaining = length;
                _initialPosition = _baseStream.Position;
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
                int toRead = (int)Math.Min(count, _remaining);
                int read = _baseStream.Read(buffer, offset, toRead);
                _remaining -= read;
                return read;
            }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                if (_remaining == 0) return 0;
                int toRead = (int)Math.Min(count, _remaining);
                int read = await _baseStream.ReadAsync(buffer.AsMemory(offset, toRead), token);
                _remaining -= read;
                return read;
            }
            public override void Flush() => _baseStream.Flush();
            public override long Seek(long offset, SeekOrigin origin)
            {
                // Simplificado para read-only forward
                long target = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => Position + offset,
                    _ => throw new NotSupportedException()
                };
                long absTarget = _initialPosition + target;
                _baseStream.Seek(absTarget, SeekOrigin.Begin);
                _remaining = Length - target;
                return target;
            }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            // DisposeAsync y Dispose vacíos para no cerrar el stream base
            public override async ValueTask DisposeAsync() => await Task.CompletedTask;
            protected override void Dispose(bool disposing) { }
        }
    }
}