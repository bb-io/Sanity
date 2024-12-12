﻿using Apps.Sanity.Constants;
using Blackbird.Applications.Sdk.Common.Authentication;
using Blackbird.Applications.Sdk.Common.Connections;

namespace Apps.Sanity.Connections;

public class ConnectionDefinition : IConnectionDefinition
{
    public IEnumerable<ConnectionPropertyGroup> ConnectionPropertyGroups => new List<ConnectionPropertyGroup>
    {
        new()
        {
            Name = "Developer API key",
            AuthenticationType = ConnectionAuthenticationType.Undefined,
            ConnectionProperties = new List<ConnectionProperty>
            {
                new(CredsNames.ProjectId)
                {
                    DisplayName = "Project ID", 
                    Description = "You can find this in your Sanity project, located below the project name."
                },
                new(CredsNames.ApiToken)
                {
                    DisplayName = "API token", 
                    Sensitive = true,
                    Description =
                        "Token can be generated in your Sanity project under the API tab. The app requires `Editor` permissions to access all functionality."
                }
            }
        }
    };

    public IEnumerable<AuthenticationCredentialsProvider> CreateAuthorizationCredentialsProviders(
        Dictionary<string, string> values) =>
        values.Select(x => new AuthenticationCredentialsProvider(x.Key, x.Value)).ToList();
}