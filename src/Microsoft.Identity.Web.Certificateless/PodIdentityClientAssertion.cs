﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Microsoft.Identity.Web
{
    /// <summary>
    /// Gets a signed assertion from PodIdentity when an app is running in a container
    /// in Azure Kubernetes Services. See https://aka.ms/ms-id-web/certificateless.
    /// </summary>
    public class PodIdentityClientAssertion : ClientAssertionProviderBase
    {
        /// <summary>
        /// Gets a signed assertion from PodIdentity. The file path is provided
        /// by an environment variable ("AZURE_FEDERATED_TOKEN_FILE")
        /// See https://aka.ms/ms-id-web/certificateless.
        /// </summary>
        public PodIdentityClientAssertion() : this(null)
        {
        }

        /// <summary>
        /// Gets a signed assertion from a file.
        /// See https://aka.ms/ms-id-web/certificateless.
        /// </summary>
        /// <param name="filePath"></param>
        public PodIdentityClientAssertion(string? filePath)
        {
            // See https://blog.identitydigest.com/azuread-federate-k8s/
            _filePath = filePath ?? Environment.GetEnvironmentVariable("AZURE_FEDERATED_TOKEN_FILE");
        }

        private readonly string _filePath;

        /// <summary>
        /// Get the signed assertion from a file.
        /// </summary>
        /// <returns>The signed assertion.</returns>
        internal override Task<ClientAssertion> GetClientAssertion(CancellationToken cancellationToken)
        {
            string signedAssertion = File.ReadAllText(_filePath);
            // Compute the expiry
            JsonWebToken jwt = new JsonWebToken(signedAssertion);
            return Task.FromResult(new ClientAssertion(signedAssertion, jwt.ValidTo));
        }
    }
}
