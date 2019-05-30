// Copyright (c) NetTopologySuite Team
// Licensed under the BSD 3-Clause license. See LICENSE.md in the project root for license information.

using System;
using System.IO;

using GeoAPI.Geometries;
using GeoAPI.IO;

using NetTopologySuite.Geometries.Implementation;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Creates a 
    /// </summary>
    public class GeoPackageGeoWriter : IBinaryGeometryWriter
    {
        private Ordinates _handleOrdinates = Ordinates.XYZM;

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
                int ordinates = 0;
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
                }
                int isEmpty = geom.IsEmpty ? 1 : 0;
                byte flags = (byte)(byteOrder + (ordinates << 1) + (isEmpty << 4));
                var header = new GeoPackageBinaryHeader
                {
                    Extent = geom.EnvelopeInternal,
                    Flags = flags,
                    SrsId = HandleSRID ? geom.SRID : -1
                };
                GeoPackageBinaryHeader.Write(writer, header);

                bool emitZ = (HandleOrdinates & Ordinates.Z) == Ordinates.Z;
                bool emitM = (HandleOrdinates & Ordinates.M) == Ordinates.M;

                if (isEmpty == 1 && geom.OgcGeometryType == OgcGeometryType.Point)
                {
                    // In GeoPackages these points SHALL be encoded as a Point where each coordinate
                    // value is set to an IEEE-754 quiet NaN value.
                    double qnan = BitConverter.Int64BitsToDouble(0x7FF8000000000000);
                    int dimension = 2 + (emitZ ? 1 : 0) + (emitM ? 1 : 0);
                    double[] ords = new double[dimension];
                    for (int i = 0; i < ords.Length; i++)
                    {
                        ords[i] = qnan;
                    }

                    geom = geom.Factory.CreatePoint(new PackedDoubleCoordinateSequence(ords, dimension));
                }

                // NOTE: GeoPackage handles SRID in its own header.  It would be invalid here.
                const bool dontHandleSRID = false;
                var wkbWriter = new WKBWriter(ByteOrder, dontHandleSRID, emitZ, emitM);
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

            // stream is disposed at the end of Write, no need to dispose it again here.
            var stream = new MemoryStream();
            Write(geom, stream);
            return stream.ToArray();
        }

        /// <inheritdoc cref="IGeometryIOSettings.HandleSRID"/>
        public bool HandleSRID
        {
            get => true;
            set
            {
                if (!value)
                    throw new InvalidOperationException("Always write SRID value!");
            }
        }

        /// <inheritdoc cref="IGeometryIOSettings.AllowedOrdinates"/>
        public Ordinates AllowedOrdinates => Ordinates.XYZM;

        /// <inheritdoc cref="IGeometryIOSettings.HandleOrdinates"/>
        public Ordinates HandleOrdinates
        {
            get => _handleOrdinates;
            set
            {
                value = Ordinates.XY | (AllowedOrdinates & value);
                _handleOrdinates = value;
            }
        }

        /// <inheritdoc cref="IBinaryGeometryWriter.ByteOrder"/>
        public ByteOrder ByteOrder
        {
            get => ByteOrder.LittleEndian;
            set
            {
                if (value != ByteOrder.LittleEndian)
                    throw new InvalidOperationException("Always use LittleEndian!");
            }
        }
    }
}
