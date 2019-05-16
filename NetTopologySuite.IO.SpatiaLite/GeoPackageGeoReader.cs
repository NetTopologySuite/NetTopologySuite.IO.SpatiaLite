// Copyright 2011 - Felix Obermaier (ivv-aachen.de)
//
// This file is part of NetTopologySuite.IO.SpatiaLite
// NetTopologySuite.IO.SpatiaLite is free software; you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// NetTopologySuite.IO.SpatiaLite is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.

// You should have received a copy of the GNU Lesser General Public License
// along with NetTopologySuite.IO.SpatiaLite if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Diagnostics;
using System.IO;
using GeoAPI;
using GeoAPI.DataStructures;
using GeoAPI.Geometries;
using GeoAPI.IO;
using SharpMap.Data.Providers.Geometry;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Class to read GeoPackage geometries from an array of bytes
    /// </summary>
    /// <seealso href="https://github.com/SharpMap/SharpMap/blob/Branches/1.0/SharpMap.Data.Providers.GeoPackage/Geometry/GpkgStandardBinary.cs"/>
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

        private static readonly Interval _full = Interval.Create(double.MinValue, double.MaxValue);

        /// <inheritdoc cref="IGeometryReader{TSource}.Read(Stream)"/>>
        public IGeometry Read(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var reader = new BinaryReader(stream))
            {
                var header = GpkgBinaryHeader.Read(reader);
                int count = (int)(reader.BaseStream.Length - reader.BaseStream.Position);
                byte[] bytes = reader.ReadBytes(count);

                var services = new NtsGeometryServices(_coordinateSequenceFactory,
                    _precisionModel, header.SrsId);
                var wkbReader = new WKBReader(services);
                var geom = wkbReader.Read(bytes);
                geom.SRID = header.SrsId;
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
                value = AllowedOrdinates & value;
                _handleOrdinates = value;
            }
        }
    }
}
