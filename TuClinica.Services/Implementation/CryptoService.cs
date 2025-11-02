using System;
using System.Security.Cryptography; // Para AES, Rfc2898DeriveBytes, etc.
using System.Text;
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class CryptoService : ICryptoService
    {
        // Mismos métodos que tenías en BackupService, pero ahora públicos

        public byte[] Encrypt(byte[] dataToEncrypt, string password)
        {
            byte[] salt = RandomNumberGenerator.GetBytes(16);
            using var keyDerivation = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
            byte[] key = keyDerivation.GetBytes(32); // AES-256
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
            const int saltSize = 16; const int nonceSize = 12; const int tagSize = 16;
            int expectedMinLength = saltSize + nonceSize + tagSize;

            if (encryptedDataWithMeta == null || encryptedDataWithMeta.Length < expectedMinLength)
            {
                return null;
            }

            try
            {
                // Extract metadata
                byte[] salt = new byte[saltSize]; byte[] nonce = new byte[nonceSize]; byte[] tag = new byte[tagSize];
                int cipherTextLength = encryptedDataWithMeta.Length - expectedMinLength;
                byte[] cipherText = new byte[cipherTextLength];
                Buffer.BlockCopy(encryptedDataWithMeta, 0, salt, 0, saltSize);
                Buffer.BlockCopy(encryptedDataWithMeta, saltSize, nonce, 0, nonceSize);
                Buffer.BlockCopy(encryptedDataWithMeta, saltSize + nonceSize, tag, 0, tagSize);
                Buffer.BlockCopy(encryptedDataWithMeta, expectedMinLength, cipherText, 0, cipherTextLength);

                // Derive key
                using var keyDerivation = new Rfc2898DeriveBytes(password, salt, 10000, HashAlgorithmName.SHA256);
                byte[] key = keyDerivation.GetBytes(32);

                // Decrypt
                using var aesGcm = new AesGcm(key);
                byte[] decryptedData = new byte[cipherText.Length];
                aesGcm.Decrypt(nonce, cipherText, tag, decryptedData);
                return decryptedData;
            }
            catch (CryptographicException ex)
            {
                return null;
            }
            catch (Exception ex)
            {
                return null; // Return null on unexpected errors too
            }
        }
    }
}