using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace Tapio.Tadamo.Clients.WebApi;

public partial interface ITadamoApiClient
{
    /// <summary>Gets a tool in xml format by internal id </summary>
    /// <param name="subscriptionId">The id of subscription.</param>
    /// <param name="id">ID of the instance.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Ok</returns>
    /// <exception cref="TadamoApiException">A server side error occurred.</exception>
    Task<XmlDocument> GetEtmlInstanceByIdXmlAsync(Guid subscriptionId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Creates an instance.
    /// </summary>
    /// <param name="subscriptionId">The id of subscription in which scope instance is created.</param>
    /// <param name="body">The instance creation request.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>Created</returns>
    /// <exception cref="TadamoApiException">A server side error occurred.</exception>
    Task<Guid> CreateEtmlInstanceAsync(Guid subscriptionId, XmlDocument body, CancellationToken cancellationToken);


    /// <summary>
    /// Retrieves the content of the specified instance data schema.
    /// </summary>
    /// <param name="typeId">Specifies the type of the instance e.g. 'Sawblades' or 'Edgeband'</param>
    /// <param name="version">The exact version of the schema e.g. 1.0.0</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A stream with the schemas content.</returns>
    Task<Stream> GetInstanceDataSchemaContentAsync(Guid typeId, string version, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves the content of the specified master data schema.
    /// </summary>
    /// <param name="typeId">Specifies the type of the masterdata e.g. 'Sawblades' or 'Edgeband'</param>
    /// <param name="version">The exact version of the schema e.g. 1.0.0</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A stream with the schemas content.</returns>
    Task<Stream> GetMasterDataSchemaContentAsync(Guid typeId, string version, CancellationToken cancellationToken);
}
