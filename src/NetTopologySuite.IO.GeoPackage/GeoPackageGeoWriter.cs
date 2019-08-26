// Copyright (c) NetTopologySuite Team
// Licensed under the BSD 3-Clause license. See LICENSE.md in the project root for license information.

using System;
using System.IO;

using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Creates a 
    /// </summary>
    public class GeoPackageGeoWriter
    {
        /// <summary>
        /// Gets the <see cref="Ordinates"/> that this class can write.
        /// </summary>
        public static readonly Ordinates AllowedOrdinates = Ordinates.XYZM;

        private Ordinates _handleOrdinates = AllowedOrdinates;

        /// <summary>
        /// Gets or sets the maximum <see cref="Ordinates"/> to write out.
        /// The default is equivalent to <see cref="AllowedOrdinates"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The purpose of this property is to <b>restrict</b> what gets written out to ensure that,
        /// e.g., Z values are never written out even if present on a geometry instance.  Ordinates
        /// that are not present on a geometry instance will be omitted regardless of this value.
        /// </para>
        /// <para>
        /// Flags not present in <see cref="AllowedOrdinates"/> are silently ignored.
        /// </para>
        /// <para>
        /// <see cref="Ordinates.X"/> and <see cref="Ordinates.Y"/> are always present.
        /// </para>
        /// </remarks>
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
        /// Serializes a given <see cref="Geometry"/> to a given <see cref="Stream"/>.
        /// </summary>
        /// <param name="geom">The <see cref="Geometry"/> to serialize.</param>
        /// <param name="stream">The <see cref="Stream"/> to write <paramref name="geom"/> to.</param>
        /// <exception cref="ArgumentNullException">Thrown when either argument is <see langword="null"/>.</exception>
        public void Write(Geometry geom, Stream stream)
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
                int byteOrder = (int)ByteOrder.LittleEndian;
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
                    SrsId = geom.SRID,
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
                    int measures = emitM ? 1 : 0;
                    double[] ords = new double[dimension];
                    for (int i = 0; i < ords.Length; i++)
                    {
                        ords[i] = qnan;
                    }

                    geom = geom.Factory.CreatePoint(new PackedDoubleCoordinateSequence(ords, dimension, measures));
                }

                // NOTE: GeoPackage handles SRID in its own header.  It would be invalid here.
                const bool dontHandleSRID = false;
                var wkbWriter = new WKBWriter(ByteOrder.LittleEndian, dontHandleSRID, emitZ, emitM);
                wkbWriter.Write(geom, stream);
            }
        }

        /// <summary>
        /// Serializes a given <see cref="Geometry"/> to a new byte array.
        /// </summary>
        /// <param name="geom">The <see cref="Geometry"/> to serialize.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="geom"/> is <see langword="null"/>.</exception>
        public byte[] Write(Geometry geom)
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
    }
}
