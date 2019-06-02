// Copyright (c) NetTopologySuite Team
// Licensed under the BSD 3-Clause license. See LICENSE.md in the project root for license information.

using System;
using System.IO;

using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Class to read GeoPackage geometries from an array of bytes
    /// </summary>
    public class GeoPackageGeoReader
    {
        private readonly PrecisionModel _precisionModel;
        private readonly CoordinateSequenceFactory _coordinateSequenceFactory;
        private Ordinates _handleOrdinates;

        /// <summary>
        /// Creates an instance of this class using the default <see cref="CoordinateSequenceFactory"/> and <see cref="PrecisionModel"/> to use.
        /// </summary>
        public GeoPackageGeoReader()
            : this(NtsGeometryServices.Instance.DefaultCoordinateSequenceFactory, NtsGeometryServices.Instance.DefaultPrecisionModel, Ordinates.XYZM)
        { }

        /// <summary>
        /// Creates an instance of this class using the provided <see cref="CoordinateSequenceFactory"/> and <see cref="PrecisionModel"/> to use.
        /// </summary>
        public GeoPackageGeoReader(CoordinateSequenceFactory coordinateSequenceFactory, PrecisionModel precisionModel)
            : this(coordinateSequenceFactory, precisionModel, Ordinates.XYZM)
        { }

        /// <summary>
        /// Creates an instance of this class using the provided <see cref="CoordinateSequenceFactory"/> and <see cref="PrecisionModel"/> to use.
        /// Additionally the ordinate values that are to be handled can be set.
        /// </summary>
        public GeoPackageGeoReader(CoordinateSequenceFactory coordinateSequenceFactory, PrecisionModel precisionModel, Ordinates handleOrdinates)
        {
            _coordinateSequenceFactory = coordinateSequenceFactory ?? throw new ArgumentNullException(nameof(coordinateSequenceFactory));
            _precisionModel = precisionModel ?? throw new ArgumentNullException(nameof(precisionModel));
            HandleOrdinates = handleOrdinates;
        }

        /// <inheritdoc cref="WKBReader.RepairRings" />
        public bool RepairRings { get; set; }

        /// <inheritdoc cref="WKBReader.HandleSRID" />
        public bool HandleSRID { get; set; }

        /// <inheritdoc cref="WKBReader.AllowedOrdinates" />
        public Ordinates AllowedOrdinates => Ordinates.XYZM & _coordinateSequenceFactory.Ordinates;

        /// <inheritdoc cref="WKBReader.HandleOrdinates" />
        public Ordinates HandleOrdinates
        {
            get => _handleOrdinates;
            set
            {
                value = Ordinates.XY | (AllowedOrdinates & value);
                _handleOrdinates = value;
            }
        }

        /// <summary>
        /// Deserializes a <see cref="Geometry"/> from the given byte array.
        /// </summary>
        /// <param name="blob">The byte array to read the geometry from.</param>
        /// <returns>The deserialized <see cref="Geometry"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="blob"/> is <see langword="null"/>.</exception>
        public Geometry Read(byte[] blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException(nameof(blob));
            }

            // stream is disposed at the end of Read, no need to dispose it again here.
            return Read(new MemoryStream(blob));
        }

        /// <summary>
        /// Deserializes a <see cref="Geometry"/> from the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream">The <see cref="Stream"/> to read the geometry from.</param>
        /// <returns>The deserialized <see cref="Geometry"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <see langword="null"/>.</exception>
        public Geometry Read(Stream stream)
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

                var wkbReader = new WKBReader(services)
                {
                    HandleOrdinates = HandleOrdinates,
                    RepairRings = RepairRings,

                    // NOTE: GeoPackage handle SRID in header, so no need to read this also in wkb;
                    HandleSRID = false,
                };
                var geom = wkbReader.Read(stream);
                if (HandleSRID)
                {
                    geom.SRID = header.SrsId;
                }

                if (header.IsEmpty && geom.OgcGeometryType == OgcGeometryType.Point)
                {
                    // In GeoPackages these points SHALL be encoded as a Point where each coordinate
                    // value is set to an IEEE-754 quiet NaN value.
                    geom = geom.Factory.CreatePoint();
                }

                return geom;
            }
        }
    }
}
