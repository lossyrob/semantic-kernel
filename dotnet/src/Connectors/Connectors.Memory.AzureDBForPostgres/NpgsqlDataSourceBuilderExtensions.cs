// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Npgsql;

namespace Microsoft.SemanticKernel.Connectors.AzureDBForPostgres;

public static class NpgsqlDataSourceBuilderExtensions
{
    private static readonly TokenRequestContext s_azureDBForPostgresTokenRequestContext = new([AzureDBForPostgresConstants.AzureDBForPostgresScope]);
    public static NpgsqlDataSourceBuilder UseEntraAuthentication(this NpgsqlDataSourceBuilder dataSourceBuilder, TokenCredential? credential = default)
    {
        // If password is provided, error out
        if (dataSourceBuilder.ConnectionStringBuilder.TryGetValue("Password", out _))
        {
            throw new ArgumentException("Password should not be provided when using Entra authentication");
        }

        credential ??= new DefaultAzureCredential();

        AccessToken? token = null;

        if (dataSourceBuilder.ConnectionStringBuilder.Username == null)
        {
            token = credential.GetToken(s_azureDBForPostgresTokenRequestContext, default);
            SetUsernameFromToken(dataSourceBuilder, token.Value.Token);
        }

        SetPasswordProvider(dataSourceBuilder, credential, token, s_azureDBForPostgresTokenRequestContext);

        return dataSourceBuilder;
    }

    public static async Task<NpgsqlDataSourceBuilder> UseEntraAuthenticationAsync(this NpgsqlDataSourceBuilder dataSourceBuilder, TokenCredential? credential = default, CancellationToken cancellationToken = default)
    {
        // If password is provided, error out
        if (dataSourceBuilder.ConnectionStringBuilder.TryGetValue("Password", out _))
        {
            throw new ArgumentException("Password should not be provided when using Entra authentication");
        }

        credential ??= new DefaultAzureCredential();

        AccessToken? token = null;

        if (dataSourceBuilder.ConnectionStringBuilder.Username == null)
        {
            token = await credential.GetTokenAsync(s_azureDBForPostgresTokenRequestContext, cancellationToken).ConfigureAwait(false);
            SetUsernameFromToken(dataSourceBuilder, token.Value.Token);
        }

        SetPasswordProvider(dataSourceBuilder, credential, token, s_azureDBForPostgresTokenRequestContext);

        return dataSourceBuilder;
    }

    private static void SetPasswordProvider(NpgsqlDataSourceBuilder dataSourceBuilder, TokenCredential credential, AccessToken? initialToken, TokenRequestContext tokenRequestContext)
    {
        AccessToken? token = initialToken;

        dataSourceBuilder.UsePeriodicPasswordProvider(async (_, ct) =>
        {
            if (token != null && token.Value.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return token.Value.Token;
            }
            token = await credential.GetTokenAsync(tokenRequestContext, ct).ConfigureAwait(false);
            return token.Value.Token;
        }, TimeSpan.FromHours(24), TimeSpan.FromSeconds(10));
    }

    private static void SetUsernameFromToken(NpgsqlDataSourceBuilder dataSourceBuilder, string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
        var claims = jsonToken?.Claims;
        var username = (
            jsonToken?.Claims.FirstOrDefault(claim => claim.Type == "upn")?.Value ??
            jsonToken?.Claims.FirstOrDefault(claim => claim.Type == "preferred_username")?.Value ??
            jsonToken?.Claims.FirstOrDefault(claim => claim.Type == "unique_name")?.Value
        );

        if (username != null)
        {
            dataSourceBuilder.ConnectionStringBuilder.Username = username;
        }
        else
        {
            throw new InvalidOperationException("Could not determine username from token claims");
        }
    }
}
