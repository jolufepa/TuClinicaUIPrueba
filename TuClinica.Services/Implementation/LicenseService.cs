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
       
        private const string PublicKey = "<RSAKeyValue><Modulus>11lRQe0V/qB6S3f9Ru2IIU+wWvrQ3qL520RoaZj5kGvmit/87p/GmrTMkHvYDQjNVlHwtRv9RgQit8pMIkaK6fid0MoSoElpO+fFZCSjL3dqpEekHo0F9vwXsxE3206Hwt/1Gs4ndNa0GQ2GyNau35jUfy9edcA0+1ysLbP4htV6lVsxJ1O8s4ujdpuimWbi5uIU0IgL0mAb2I0m3tRu7dJf7sLjqN1JXvVI8OtrCHLvs/kQnlEGE4LnVdlOxvcOFZW2bqY9OtPyX0753N+Y85Y8W+C/vZfPiCI4GUTc/jdXtcIg8Iro0qpIqL9oi0erBxT8WbqwOU+c+vwm75iNxQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private readonly string _licenseFilePath;
        private readonly IFileSystemService _fileSystemService;

        public LicenseService(IFileSystemService fileSystemService)
        {
            string baseDir = AppContext.BaseDirectory;
            _licenseFilePath = Path.Combine(baseDir, "license.dat");
            _fileSystemService = fileSystemService;
        }

        public virtual string GetMachineIdString()
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
            if (!_fileSystemService.FileExists(_licenseFilePath))
            {
                return false;
            }

            try
            {
                string fileContent = _fileSystemService.ReadAllText(_licenseFilePath);
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