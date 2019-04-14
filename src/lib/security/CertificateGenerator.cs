﻿//******************************************************************************************************
//  CertificateGenerator.cs - Gbtc
//
//  Copyright © 2019, Grid Protection Alliance.  All Rights Reserved.
//
//  Licensed to the Grid Protection Alliance (GPA) under one or more contributor license agreements. See
//  the NOTICE file distributed with this work for additional information regarding copyright ownership.
//  The GPA licenses this file to you under the MIT License (MIT), the "License"; you may not use this
//  file except in compliance with the License. You may obtain a copy of the License at:
//
//      http://opensource.org/licenses/MIT
//
//  Unless agreed to in writing, the subject software distributed under the License is distributed on an
//  "AS-IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. Refer to the
//  License for the specific language governing permissions and limitations.
//
//  Code Modification History:
//  ----------------------------------------------------------------------------------------------------
//  04/14/2019 - J. Ritchie Carroll
//       Imported source code from Grid Solutions Framework.
//
//******************************************************************************************************

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace sttp.security
{
    /// <summary>
    /// The CertificateGenerator searches certificate stores for existing certificates and creates
    /// self-signed certificates if no matching certificate exists in any accessible store.
    /// It then generates a certificate file at the specified certificate path.
    /// </summary>
    public class CertificateGenerator
    {
        #region [ Members ]

        // Fields
        private string m_issuer;
        private string[] m_subjectNames;
        private string m_certificatePath;
        private readonly List<string> m_debugLog;

        #endregion

        #region [ Constructors ]

        /// <summary>
        /// Creates a new instance of the <see cref="CertificateGenerator"/> class.
        /// </summary>
        public CertificateGenerator()
        {
            m_debugLog = new List<string>();
        }

        #endregion

        #region [ Properties ]

        /// <summary>
        /// Gets or sets the name of the entity issuing the certificate.
        /// </summary>
        public string Issuer
        {
            get => (m_issuer = m_issuer ?? GetDefaultIssuer());
            set => m_issuer = value;
        }

        /// <summary>
        /// Gets or sets the subject names (common names)
        /// of the entity that this certificate identifies.
        /// </summary>
        public string[] SubjectNames
        {
            get => (m_subjectNames = m_subjectNames ?? GetDefaultSubjectNames());
            set => m_subjectNames = value;
        }

        /// <summary>
        /// Gets or sets the path to the certificate file
        /// that is generated by this certificate generator.
        /// </summary>
        public string CertificatePath
        {
            get => (m_certificatePath = m_certificatePath ?? GetDefaultCertificatePath());
            set => m_certificatePath = value;
        }

        /// <summary>
        /// Gets a list of detailed log messages
        /// </summary>
        public List<string> DebugLog => new List<string>(m_debugLog);

        #endregion

        #region [ Methods ]

        /// <summary>
        /// Generates the certificate.
        /// </summary>
        /// <returns>The certificate that was generated by this certificate generator.</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        public X509Certificate2 GenerateCertificate()
        {
            List<X509Store> stores;
            List<X509Certificate2> storedCertificates;

            X509Certificate2 certificate = null;
            string certificatePath;
            string commonNameList;
            byte[] certificateData;

            ProcessStartInfo processInfo;
            string makeCertPath;

            m_debugLog.Clear();

            stores = new List<X509Store>()
            {
                new X509Store(StoreName.My, StoreLocation.LocalMachine),
                new X509Store(StoreName.My, StoreLocation.CurrentUser)
            };

            // Attempt to get an existing certificate from the given certificate path
            certificatePath = FilePath.GetAbsolutePath(CertificatePath);

            if (File.Exists(certificatePath))
                m_debugLog.Add($"Searching for existing certificate...found ({certificatePath})");
            else
                m_debugLog.Add($"Searching for existing certificate...not found ({certificatePath})");

            if (File.Exists(certificatePath))
            {
                try
                {
                    certificate = new X509Certificate2(certificatePath);
                }
                catch (CryptographicException ex)
                {
                    string message = new StringBuilder()
                        .AppendLine()
                        .AppendLine("Error opening existing certificate:")
                        .AppendLine(ex.ToString())
                        .AppendLine()
                        .ToString();

                    m_debugLog.Add(message);
                }
            }

            try
            {
                TryOpenStores(stores, OpenFlags.ReadOnly);

                if (stores.Count == 0)
                    m_debugLog.Add("Opening certificate stores with readonly access...failed");
                else if (stores.Count == 1)
                    m_debugLog.Add($"Opening certificate stores with readonly access...opened {stores[0].Location} store");
                else
                    m_debugLog.Add("Opening certificate stores with readonly access...success");

                // If a valid certificate exists on the certificate path,
                // search the certificate stores to determine if we have
                // access to its private key
                if ((object)certificate != null)
                {
                    bool canAccessPrivateKey;

                    storedCertificates = stores.SelectMany(store => store.Certificates.Cast<X509Certificate2>()).ToList();
                    FindMatchingCertificates(storedCertificates, certificate);
                    canAccessPrivateKey = storedCertificates.Any(CanAccessPrivateKey);

                    if (canAccessPrivateKey)
                        m_debugLog.Add("Attempting to access existing certificate\'s private key...success");
                    else
                        m_debugLog.Add("Attempting to access existing certificate\'s private key...failed");

                    if (canAccessPrivateKey)
                        return certificate;
                }

                // Search the certificate stores for certificates
                // with a matching issuer and accessible private keys
                commonNameList = GetCommonNameList();
                storedCertificates = stores.SelectMany(store => store.Certificates.Cast<X509Certificate2>()).ToList();
                certificate = storedCertificates.FirstOrDefault(storedCertificate => storedCertificate.Issuer.Equals(commonNameList) && CanAccessPrivateKey(storedCertificate));

                if ((object)certificate != null)
                    m_debugLog.Add("Searching stores for a usable certificate with accessible private key...success");
                else
                    m_debugLog.Add("Searching stores for a usable certificate with accessible private key...failed");

                // If such a certificate exists, generate the certificate file and return the result
                if ((object)certificate != null)
                {
                    using (FileStream certificateStream = File.OpenWrite(certificatePath))
                    {
                        certificateData = certificate.Export(X509ContentType.Cert);
                        certificateStream.Write(certificateData, 0, certificateData.Length);
                    }

                    return new X509Certificate2(certificatePath);
                }
            }
            finally
            {
                CloseStores(stores);
            }

            try
            {
                // Ensure that we can write to the certificate
                // stores before generating a new certificate
                TryOpenStores(stores, OpenFlags.ReadWrite);

                if (stores.Count == 0)
                    m_debugLog.Add("Opening certificate stores with read-write access...failed");
                else if (stores.Count == 1)
                    m_debugLog.Add($"Opening certificate stores with read-write access...opened {stores[0].Location} store");
                else
                    m_debugLog.Add("Opening certificate stores with read-write access...success");
            }
            finally
            {
                CloseStores(stores);
            }

            // Attempt to use makecert to create a new self-signed certificate
            makeCertPath = FilePath.GetAbsolutePath("makecert.exe");

            if (File.Exists(makeCertPath))
                m_debugLog.Add("Searching for makecert utility to generate a new certificate...success");
            else
                m_debugLog.Add("Searching for makecert utility to generate a new certificate...failed");

            if (File.Exists(makeCertPath))
            {
                foreach (X509Store store in stores)
                {
                    processInfo = new ProcessStartInfo(makeCertPath);
                    processInfo.Arguments = $"-r -pe -n \"{commonNameList}\" -ss My -sr {store.Location} \"{certificatePath}\"";
                    processInfo.UseShellExecute = true;

                    using (Process makeCertProcess = Process.Start(processInfo))
                    {
                        makeCertProcess?.WaitForExit();
                    }

                    if (File.Exists(certificatePath))
                        m_debugLog.Add($"Attemping to generate a new certificate in the {store.Location} store... success");
                    else
                        m_debugLog.Add($"Attemping to generate a new certificate in the {store.Location} store... failed");

                    if (File.Exists(certificatePath))
                        return new X509Certificate2(certificatePath);
                }
            }

            // All attempts to generate the certificate failed, so we must throw an exception
            throw new InvalidOperationException("Unable to generate the self-signed certificate.");
        }

        /// <summary>
        /// Exports the private key of the certificate that is currently in use to the given path,
        /// encrypting the file with the given password.
        /// </summary>
        /// <param name="exportPath">The path to which the certificate will be exported.</param>
        /// <param name="password">The password used to encrypt the certificate file.</param>
        public void ExportCertificateWithPrivateKey(string exportPath, SecureString password)
        {
            // Attempt to get an existing certificate from the given certificate path
            string certificatePath = FilePath.GetAbsolutePath(CertificatePath);
            X509Certificate2 certificate = new X509Certificate2(certificatePath);

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadOnly);

                List<X509Certificate2> certificates = store.Certificates.Cast<X509Certificate2>().ToList();
                FindMatchingCertificates(certificates, certificate);

                X509Certificate2 pvkCertificate = certificates.FirstOrDefault(CanAccessPrivateKey);

                if ((object)pvkCertificate == null)
                    throw new InvalidOperationException("Could not locate private key in the current user store.");

                byte[] certificateData = pvkCertificate.Export(X509ContentType.Pfx, password);
                File.Delete(exportPath);
                File.WriteAllBytes(exportPath, certificateData);

                store.Close();
            }
        }

        /// <summary>
        /// Imports the given certificate into the current user store and then exports the public key.
        /// </summary>
        /// <param name="importPath">The file with the certificate and private key to be imported.</param>
        /// <param name="password">The password used to encrypt the certificate file with the private key.</param>
        public void ImportCertificateWithPrivateKey(string importPath, SecureString password)
        {
            X509KeyStorageFlags keyStorageFlags = X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet;
            X509Certificate2 certificate = new X509Certificate2(importPath, password, keyStorageFlags);

            using (X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                store.Open(OpenFlags.ReadWrite);
                store.Add(certificate);
                store.Close();
            }

            string certificatePath = FilePath.GetAbsolutePath(CertificatePath);
            byte[] certificateData = certificate.Export(X509ContentType.Cert);
            File.WriteAllBytes(certificatePath, certificateData);
        }

        // Gets the list of common names to be passed to
        // makecert when generating self-signed certificates.
        private string GetCommonNameList()
        {
            return string.Join(",", new[] { Issuer }.Concat(SubjectNames).Distinct().Select(name => "CN=" + name));
        }

        // Gets the default value for the issuer.
        private string GetDefaultIssuer()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).HostName;
        }

        // Gets the default value for the subject names.
        // This uses a DNS lookup to determine the host name of the system and
        // all the possible IP addresses and aliases that the system may go by.
        private string[] GetDefaultSubjectNames()
        {
            IPHostEntry hostEntry = Dns.GetHostEntry(Dns.GetHostName());

            return hostEntry.AddressList
#if !MONO
                .Select(address => address.MapToIPv6())
#endif
                .Concat(hostEntry.AddressList)
                .Distinct()
                .Select(address => address.ToString())
                .Concat(hostEntry.Aliases)
                .Concat(new[] { Environment.MachineName, hostEntry.HostName })
                .ToArray();
        }

        // Gets the default path to which the certificate file will be generated.
        private string GetDefaultCertificatePath()
        {
            return $"{Issuer}.cer";
        }

        // Attempts to open all the stores in the given list of stores.
        // After returning, the list of stores contains only the stores which could be opened.
        private void TryOpenStores(List<X509Store> stores, OpenFlags flags)
        {
            for (int i = stores.Count - 1; i >= 0; i--)
            {
                try
                {
                    stores[i].Open(flags);
                }
                catch
                {
                    stores.RemoveAt(i);
                }
            }
        }

        // Filters the given list of certificates down to only the certificates that match the given certificate.
        private void FindMatchingCertificates(List<X509Certificate2> certificates, X509Certificate2 certificate)
        {
            byte[] hash = certificate.GetCertHash();
            byte[] key = certificate.GetPublicKey();

            for (int i = certificates.Count - 1; i >= 0; i--)
            {
                if (!hash.SequenceEqual(certificates[i].GetCertHash()) || !key.SequenceEqual(certificates[i].GetPublicKey()))
                    certificates.RemoveAt(i);
            }
        }

        // Determines if the current application has access to the private key of the given certificate.
        private bool CanAccessPrivateKey(X509Certificate2 certificate)
        {
            try
            {
                // The point here is not only to check if the certificate has a private key,
                // but also to attempt to access its private key, since doing so might result
                // in a CryptographicException; certificate.HasPrivateKey will not work
                return ((object)certificate.PrivateKey != null);
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        // Closes all stores in the given list of stores.
        private void CloseStores(List<X509Store> stores)
        {
            foreach (X509Store store in stores)
                store.Close();
        }

        #endregion
    }
}
