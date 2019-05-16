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
using System.IO;
using GeoAPI.Geometries;
using GeoAPI.IO;
using SharpMap.Data.Providers.Geometry;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Creates a 
    /// </summary>
    public class GeoPackageGeoWriter : IBinaryGeometryWriter
    {
        private Ordinates _handleOrdinates;

        /// <inheritdoc cref="IGeometryWriter{TSink}.Write(IGeometry, Stream)"/>>
        public void Write(IGeometry geom, Stream stream)
        {
            if (geom == null)
            {
                throw new ArgumentNullException(nameof(geom));
            }
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            using (var writer = new BinaryWriter(stream))
            {
                int byteOrder = (int)ByteOrder;
                int ordinates;
                switch (HandleOrdinates)
                {
                    case Ordinates.None:
                        ordinates = 0;
                        break;
                    case Ordinates.XY:
                        ordinates = 1;
                        break;
                    case Ordinates.XYZ:
                        ordinates = 2;
                        break;
                    case Ordinates.XYM:
                        ordinates = 3;
                        break;
                    case Ordinates.XYZM:
                        ordinates = 4;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("HandleOrdinates");
                }
                int isEmpty = geom.IsEmpty ? 1 : 0;
                byte flags = (byte)(byteOrder + (ordinates << 1) + (isEmpty << 4));
                var header = new GpkgBinaryHeader
                {
                    Extent = geom.EnvelopeInternal,
                    Flags = flags
                };
                GpkgBinaryHeader.Write(writer, header);

                var wkbWriter = new WKBWriter(ByteOrder);
                wkbWriter.Write(geom, stream);
            }
        }

        /// <inheritdoc cref="IGeometryWriter{TSink}.Write(IGeometry)"/>>
        public byte[] Write(IGeometry geom)
        {
            if (geom == null)
            {
                throw new ArgumentNullException(nameof(geom));
            }

            using (var stream = new MemoryStream())
            {
                Write(geom, stream);
                return stream.ToArray();
            }
        }

        /// <inheritdoc cref="IGeometryIOSettings.HandleSRID"/>
        public bool HandleSRID
        {
            get { return true; }
            set
            {
                if (!value)
                    throw new InvalidOperationException("Always write SRID value!");
            }
        }

        /// <inheritdoc cref="IGeometryIOSettings.AllowedOrdinates"/>
        public Ordinates AllowedOrdinates
        {
            get { return Ordinates.XYZM; }
        }

        /// <inheritdoc cref="IGeometryIOSettings.HandleOrdinates"/>
        public Ordinates HandleOrdinates
        {
            get { return _handleOrdinates; }
            set
            {
                value = AllowedOrdinates & value;
                _handleOrdinates = value;
            }
        }

        /// <inheritdoc cref="IBinaryGeometryWriter.ByteOrder"/>
        public ByteOrder ByteOrder
        {
            get { return ByteOrder.LittleEndian; }
            set
            {
                if (value != ByteOrder.LittleEndian)
                    throw new InvalidOperationException("Always use LittleEndian!");
            }
        }
    }
}
