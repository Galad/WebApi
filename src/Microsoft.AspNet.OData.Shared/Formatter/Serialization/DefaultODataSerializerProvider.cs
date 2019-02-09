// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNet.OData.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using ODataPath = Microsoft.AspNet.OData.Routing.ODataPath;

namespace Microsoft.AspNet.OData.Formatter.Serialization
{
    /// <summary>
    /// The default <see cref="ODataSerializerProvider"/>.
    /// </summary>
    public partial class DefaultODataSerializerProvider : ODataSerializerProvider
    {
        private readonly IServiceProvider _rootContainer;
        private readonly Lazy<ODataEnumSerializer> _enumSerializer;
        private readonly Lazy<ODataPrimitiveSerializer> _primitiveSerializer;
        private readonly Lazy<ODataDeltaFeedSerializer> _deltaFeedSerializer;
        private readonly Lazy<ODataResourceSetSerializer> _resourceSetSerializer;
        private readonly Lazy<ODataCollectionSerializer> _collectionSerializer;
        private readonly Lazy<ODataResourceSerializer> _resourceSerializer;
        private readonly Lazy<ODataServiceDocumentSerializer> _serviceDocumenteSerializer;
        private readonly Lazy<ODataEntityReferenceLinkSerializer> _entityReferenceLinkSerializer;
        private readonly Lazy<ODataEntityReferenceLinksSerializer> _entityReferenceLinksSerializer;
        private readonly Lazy<ODataErrorSerializer> _errorSerializer;
        private readonly Lazy<ODataMetadataSerializer> _metadataSerializer;
        private readonly Lazy<ODataRawValueSerializer> _rawValueSerializer;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultODataSerializerProvider"/> class.
        /// </summary>
        /// <param name="rootContainer">The root container.</param>
        public DefaultODataSerializerProvider(IServiceProvider rootContainer)
        {
            if (rootContainer == null)
            {
                throw Error.ArgumentNull("rootContainer");
            }

            _rootContainer = rootContainer;
            _enumSerializer = new Lazy<ODataEnumSerializer>(() => _rootContainer.GetRequiredService<ODataEnumSerializer>());
            _primitiveSerializer = new Lazy<ODataPrimitiveSerializer>(() => _rootContainer.GetRequiredService<ODataPrimitiveSerializer>());
            _deltaFeedSerializer = new Lazy<ODataDeltaFeedSerializer>(() => _rootContainer.GetRequiredService<ODataDeltaFeedSerializer>());
            _resourceSetSerializer = new Lazy<ODataResourceSetSerializer>(() => _rootContainer.GetRequiredService<ODataResourceSetSerializer>());
            _collectionSerializer = new Lazy<ODataCollectionSerializer>(() => _rootContainer.GetRequiredService<ODataCollectionSerializer>());
            _resourceSerializer = new Lazy<ODataResourceSerializer>(() => _rootContainer.GetRequiredService<ODataResourceSerializer>());
            _serviceDocumenteSerializer = new Lazy<ODataServiceDocumentSerializer>(() => _rootContainer.GetRequiredService<ODataServiceDocumentSerializer>());
            _entityReferenceLinkSerializer = new Lazy<ODataEntityReferenceLinkSerializer>(() => _rootContainer.GetRequiredService<ODataEntityReferenceLinkSerializer>());
            _entityReferenceLinksSerializer = new Lazy<ODataEntityReferenceLinksSerializer>(() => _rootContainer.GetRequiredService<ODataEntityReferenceLinksSerializer>());
            _errorSerializer = new Lazy<ODataErrorSerializer>(() => _rootContainer.GetRequiredService<ODataErrorSerializer>());
            _metadataSerializer = new Lazy<ODataMetadataSerializer>(() => _rootContainer.GetRequiredService<ODataMetadataSerializer>());
            _rawValueSerializer = new Lazy<ODataRawValueSerializer>(() => _rootContainer.GetRequiredService<ODataRawValueSerializer>());
        }

        /// <inheritdoc />
        public override ODataEdmTypeSerializer GetEdmTypeSerializer(IEdmTypeReference edmType)
        {
            if (edmType == null)
            {
                throw Error.ArgumentNull("edmType");
            }

            switch (edmType.TypeKind())
            {
                case EdmTypeKind.Enum:
                    return _enumSerializer.Value;

                case EdmTypeKind.Primitive:
                    return _primitiveSerializer.Value;

                case EdmTypeKind.Collection:
                    IEdmCollectionTypeReference collectionType = edmType.AsCollection();
                    if (collectionType.Definition.IsDeltaFeed())
                    {
                        return _deltaFeedSerializer.Value;
                    }
                    else if (collectionType.ElementType().IsEntity() || collectionType.ElementType().IsComplex())
                    {
                        return _resourceSetSerializer.Value;
                    }
                    else
                    {
                        return _collectionSerializer.Value;
                    }

                case EdmTypeKind.Complex:
                case EdmTypeKind.Entity:
                    return _resourceSerializer.Value;

                default:
                    return null;
            }
        }

        /// <inheritdoc />
        internal ODataSerializer GetODataPayloadSerializerImpl(Type type, Func<IEdmModel> modelFunction, ODataPath path, Type errorType)
        {
            if (type == null)
            {
                throw Error.ArgumentNull("type");
            }
            if (modelFunction == null)
            {
                throw Error.ArgumentNull("modelFunction");
            }

            // handle the special types.
            if (type == typeof(ODataServiceDocument))
            {
                return _serviceDocumenteSerializer.Value;
            }
            else if (type == typeof(Uri) || type == typeof(ODataEntityReferenceLink))
            {
                return _entityReferenceLinkSerializer.Value;
            }
            else if (TypeHelper.IsTypeAssignableFrom(typeof(IEnumerable<Uri>), type) || type == typeof(ODataEntityReferenceLinks))
            {
                return _entityReferenceLinksSerializer.Value;
            }
            else if (type == typeof(ODataError) || type == errorType)
            {
                return _errorSerializer.Value;
            }
            else if (TypeHelper.IsTypeAssignableFrom(typeof(IEdmModel), type))
            {
                return _metadataSerializer.Value;
            }

            // Get the model. Using a Func<IEdmModel> to delay evaluation of the model
            // until after the above checks have passed.
            IEdmModel model = modelFunction();

            // if it is not a special type, assume it has a corresponding EdmType.
            ClrTypeCache typeMappingCache = model.GetTypeMappingCache();
            IEdmTypeReference edmType = typeMappingCache.GetEdmType(type, model);

            if (edmType != null)
            {
                bool isCountRequest = path != null && path.Segments.LastOrDefault() is CountSegment;
                bool isRawValueRequest = path != null && path.Segments.LastOrDefault() is ValueSegment;

                if (((edmType.IsPrimitive() || edmType.IsEnum()) && isRawValueRequest) || isCountRequest)
                {
                    return _rawValueSerializer.Value;
                }
                else
                {
                    return GetEdmTypeSerializer(edmType);
                }
            }
            else
            {
                return null;
            }
        }
    }
}
