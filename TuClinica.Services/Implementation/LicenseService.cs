using System;
using System.IO;
using System.Management; // La referencia NuGet
using System.Security.Cryptography;
using System.Text;
using TuClinica.Core.Interfaces.Services;

namespace TuClinica.Services.Implementation
{
    public class LicenseService : ILicenseService
    {
        // Pega aquí el contenido COMPLETO de PublicKey.xml
        private const string PublicKey = "<RSAKeyValue><Modulus>snGRwTgPekWoChK9PtvraFBNDjsJCNwF+8HmgYoifatUZT7y91m9w9skD3zAJ0fmieQGWd8xRN219fgmIHbBB/m894tZvP5dGhFsBHbsy0FZeeH2A4AdV6AukA7o8YD6JgS6E50PgGKZikRgEE8XKec0Jd/9HYHxbX4ANtpavVmRf7HVPeN5TAIFDXDT8aq0C1rAeWr6UoYH4IEBE2aQMpySqwdctHibvdm8YI2eP6hTwiH2MuHTxk9hSYA6l4Q9tmPhAdxfj5lVBTc1nqzNFjTUsREngYjX2ToITqwTTAieGXhPOsVA4xMjO8SfrZbB7TOWTzX7ZgRRrimXSjvBnQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private readonly string _licenseFilePath;

        public LicenseService()
        {
            string baseDir = AppContext.BaseDirectory;
            _licenseFilePath = Path.Combine(baseDir, "license.dat");
        }

        public string GetMachineIdString()
        {
            try
            {
                string cpuId = GetHardwareId("Win32_Processor", "ProcessorId");
                string mbId = GetHardwareId("Win32_BaseBoard", "SerialNumber");
                string combinedId = $"CPU:{cpuId}-MB:{mbId}";

                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combinedId));
                    return BitConverter.ToString(hashBytes).Replace("-", "").Substring(0, 16);
                }
            }
            catch (Exception ex)
            {
                // Loggear el error sería buena idea aquí
                return "Error_No_HW_ID: " + Environment.MachineName;
            }
        }

        private string GetHardwareId(string wmiClass, string property)
        {
            string result = "";
            try
            {
                var options = new ConnectionOptions { Timeout = TimeSpan.FromSeconds(5) };
                var scope = new ManagementScope(@"\\.\root\cimv2", options);
                scope.Connect();

                var query = new ObjectQuery($"SELECT {property} FROM {wmiClass}");
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        result = obj[property]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(result))
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Loggear el error sería buena idea aquí
            }
            return result;
        }

        public bool IsLicenseValid()
        {
            if (!File.Exists(_licenseFilePath))
            {
                return false;
            }

            try
            {
                string fileContent = File.ReadAllText(_licenseFilePath);
                string[] parts = fileContent.Split(new[] { "\n--SIGNATURE--\n" }, StringSplitOptions.None);

                if (parts.Length != 2) return false;

                string base64LicenseData = parts[0];
                string base64Signature = parts[1];

                byte[] licenseBytes = Convert.FromBase64String(base64LicenseData);
                byte[] signatureBytes = Convert.FromBase64String(base64Signature);

                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(PublicKey);

                    // *** CORRECCIÓN AQUÍ: Orden correcto de argumentos ***
                    // Antes era: VerifyData(licenseBytes, HashAlgorithmName.SHA256, signatureBytes, ...)
                    bool isSignatureValid = rsa.VerifyData(licenseBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);


                    if (!isSignatureValid) return false;
                }

                string licenseDataString = Encoding.UTF8.GetString(licenseBytes);
                string? licensedMachineId = null;

                var dataParts = licenseDataString.Split(';');
                foreach (var part in dataParts)
                {
                    if (part.StartsWith("MachineID="))
                    {
                        licensedMachineId = part.Substring("MachineID=".Length);
                        break;
                    }
                }

                if (string.IsNullOrEmpty(licensedMachineId)) return false;

                string currentMachineId = GetMachineIdString();

                return licensedMachineId.Equals(currentMachineId, StringComparison.OrdinalIgnoreCase);

            }
            catch (FormatException) { return false; }
            catch (CryptographicException) { return false; }
            catch (Exception ex) { return false; }
        }
    }
}