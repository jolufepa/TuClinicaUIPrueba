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
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int HmacSize = 32;
        private const int KeySize = 32;
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

        public async Task DecryptAsync(Stream inputStream, Stream outputStream, string password)
        {
            if (!inputStream.CanSeek)
                throw new ArgumentException("El stream de entrada debe ser 'Seekable' para soportar la detección de formato.");

            long startPosition = inputStream.Position;

            try
            {
                await DecryptWithHmacAsync(inputStream, outputStream, password);
            }
            catch (Exception)
            {
                inputStream.Seek(startPosition, SeekOrigin.Begin);

                if (outputStream.CanSeek)
                {
                    outputStream.SetLength(0);
                }

                try
                {
                    await DecryptLegacyAsync(inputStream, outputStream, password);
                }
                catch (CryptographicException ex)
                {
                    throw new Exception("La contraseña es incorrecta o el formato de la base de datos no es compatible.", ex);
                }
            }
        }

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

        private async Task DecryptLegacyAsync(Stream inputStream, Stream outputStream, string password)
        {
            long startPosition = inputStream.Position;
            Exception lastError = null;

            try
            {
                inputStream.Seek(startPosition, SeekOrigin.Begin);
                if (outputStream.CanSeek) outputStream.SetLength(0);

                byte[] iv = new byte[IvSize];
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
                    return;
                }
            }
            catch (Exception ex) { lastError = ex; }

            var configs = new[]
            {
                new { Iterations = 1000, Algo = HashAlgorithmName.SHA1 },
                new { Iterations = 10000, Algo = HashAlgorithmName.SHA256 },
                new { Iterations = 2, Algo = HashAlgorithmName.SHA1 }
            };

            byte[] salt = new byte[SaltSize];
            byte[] iv2 = new byte[IvSize];

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
                        return;
                    }
                    catch (Exception ex) { lastError = ex; }
                }
            }

            try
            {
                inputStream.Seek(startPosition, SeekOrigin.Begin);
                if (outputStream.CanSeek) outputStream.SetLength(0);

                using var sha256 = SHA256.Create();
                byte[] key = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                byte[] zeroIv = new byte[16];

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

            public override async ValueTask DisposeAsync() => await Task.CompletedTask;
            protected override void Dispose(bool disposing) { }
        }
    }
}