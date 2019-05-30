// Copyright (c) NetTopologySuite Team
// Licensed under the BSD 3-Clause license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using GeoAPI;
using GeoAPI.Geometries;
using GeoAPI.IO;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Class to read GeoPackage geometries from an array of bytes
    /// </summary>
    public class GeoPackageGeoReader : IBinaryGeometryReader
    {
        private readonly IPrecisionModel _precisionModel;
        private readonly ICoordinateSequenceFactory _coordinateSequenceFactory;
        private Ordinates _handleOrdinates;

        /// <summary>
        /// Creates an instance of this class using the default <see cref="ICoordinateSequenceFactory"/> and <see cref="IPrecisionModel"/> to use.
        /// </summary>
        public GeoPackageGeoReader()
            : this(GeometryServiceProvider.Instance.DefaultCoordinateSequenceFactory, GeometryServiceProvider.Instance.DefaultPrecisionModel)
        { }

        /// <summary>
        /// Creates an instance of this class using the provided <see cref="ICoordinateSequenceFactory"/> and <see cref="IPrecisionModel"/> to use.
        /// </summary>
        public GeoPackageGeoReader(ICoordinateSequenceFactory coordinateSequenceFactory, IPrecisionModel precisionModel)
            : this(coordinateSequenceFactory, precisionModel, Ordinates.XYZM)
        { }

        /// <summary>
        /// Creates an instance of this class using the provided <see cref="ICoordinateSequenceFactory"/> and <see cref="IPrecisionModel"/> to use.
        /// Additionally the ordinate values that are to be handled can be set.
        /// </summary>
        public GeoPackageGeoReader(ICoordinateSequenceFactory coordinateSequenceFactory, IPrecisionModel precisionModel, Ordinates handleOrdinates)
        {
            _coordinateSequenceFactory = coordinateSequenceFactory;
            _precisionModel = precisionModel;
            _handleOrdinates = handleOrdinates;
        }

        /// <inheritdoc cref="IGeometryReader{TSource}.Read(TSource)"/>>
        public IGeometry Read(byte[] blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException(nameof(blob));
            }

            using (var stream = new MemoryStream(blob))
            {
                return Read(stream);
            }
        }

        /// <inheritdoc cref="IGeometryReader{TSource}.Read(Stream)"/>>
        public IGeometry Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var reader = new BinaryReader(stream))
            {
                var header = GeoPackageBinaryHeader.Read(reader);
                var services = new NtsGeometryServices(_coordinateSequenceFactory,
                    _precisionModel, HandleSRID ? header.SrsId : -1);
                // NOTE: GeoPackage handle SRID in header, so no need to read this also in wkb;
                const bool dontHandleSRID = false;
                var wkbReader = new WKBReader(services)
                {
                    HandleSRID = dontHandleSRID,
                    HandleOrdinates = HandleOrdinates,
                    RepairRings = RepairRings
                };
                var geom = wkbReader.Read(stream);
                if (HandleSRID)
                {
                    geom.SRID = header.SrsId;
                }
                return geom;
            }
        }

        /// <inheritdoc cref="IGeometryReader{TSource}.RepairRings" />
        public bool RepairRings { get; set; }

        /// <inheritdoc cref="IGeometryIOSettings.HandleSRID"/>>
        public bool HandleSRID { get; set; }

        /// <inheritdoc cref="IGeometryIOSettings.AllowedOrdinates"/>>
        public Ordinates AllowedOrdinates
        {
            get { return Ordinates.XYZM; }
        }

        /// <inheritdoc cref="IGeometryIOSettings.HandleOrdinates"/>>
        public Ordinates HandleOrdinates
        {
            get { return _handleOrdinates; }
            set
            {
                value = Ordinates.XY | (AllowedOrdinates & value);
                _handleOrdinates = value;
            }
        }
    }
}
