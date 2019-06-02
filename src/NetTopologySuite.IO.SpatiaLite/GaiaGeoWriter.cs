// Copyright (c) Felix Obermaier (ivv-aachen.de) and the NetTopologySuite Team
// Licensed under the BSD 3-Clause license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Creates a 
    /// </summary>
    public class GaiaGeoWriter
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
        /// Gets or sets whether geometries should be written in compressed form
        /// </summary>
        public bool UseCompressed { get; set; }

        public void Write(Geometry geometry, Stream stream)
        {
            var g = Write(geometry);
            stream.Write(g, 0, g.Length);
        }

        public byte[] Write(Geometry geom)
        {
            //if (geom.IsEmpty)
            //    return GaiaGeoEmptyHelper.EmptyGeometryCollectionWithSrid(geom.SRID);

            var hasZ = (HandleOrdinates & Ordinates.Z) == Ordinates.Z;
            var hasM = (HandleOrdinates & Ordinates.M) == Ordinates.M;

            var gaiaExport = SetGaiaGeoExportFunctions(GaiaGeoEndianMarker.GAIA_LITTLE_ENDIAN, hasZ, hasM, UseCompressed);

            using (var ms = new MemoryStream())
            {
                using (var bw = new BinaryWriter(ms))
                {
                    //Header
                    bw.Write((byte)GaiaGeoBlobMark.GAIA_MARK_START);
                    bw.Write((byte)GaiaGeoEndianMarker.GAIA_LITTLE_ENDIAN);
                    //SRID
                    gaiaExport.WriteInt32(bw, geom.SRID);
                    //MBR
                    var env = geom.EnvelopeInternal; //.Coordinates;
                    if (geom.IsEmpty)
                    {
                        gaiaExport.WriteDouble(bw, 0d, 0d, 0d, 0d);
                    }
                    else
                    {
                        gaiaExport.WriteDouble(bw, env.MinX, env.MinY, env.MaxX, env.MaxY);
                    }

                    bw.Write((byte)GaiaGeoBlobMark.GAIA_MARK_MBR);

                    //Write geometry
                    WriteGeometry(geom, gaiaExport, bw);

                    bw.Write((byte)GaiaGeoBlobMark.GAIA_MARK_END);
                }
                return ms.ToArray();
            }
        }

        private static WriteCoordinates SetWriteCoordinatesFunction(GaiaExport gaiaExport)
        {
            if (gaiaExport.Uncompressed)
            {
                if (gaiaExport.HasZ && gaiaExport.HasM)
                    return WriteXYZM;
                if (gaiaExport.HasM)
                    return WriteXYM;
                if (gaiaExport.HasZ)
                    return WriteXYZ;

                return WriteXY;
            }

            if (gaiaExport.HasZ && gaiaExport.HasM)
                return WriteCompressedXYZM;
            if (gaiaExport.HasM)
                return WriteCompressedXYM;
            if (gaiaExport.HasZ)
                return WriteCompressedXYZ;

            return WriteCompressedXY;
        }

        private static void WriteGeometry(Geometry geom, GaiaExport gaiaExport, BinaryWriter bw)
        {
            WriteCoordinates writeCoordinates = SetWriteCoordinatesFunction(gaiaExport);

            //Geometry type
            int coordinateFlag = gaiaExport.CoordinateFlag;
            int coordinateFlagNotValidForCompression = coordinateFlag > 1000000
                                                           ? coordinateFlag - 1000000
                                                           : coordinateFlag;
            switch (geom.OgcGeometryType)
            {
                case OgcGeometryType.Point:
                    gaiaExport.WriteInt32(bw, (int)(GaiaGeoGeometry.GAIA_POINT) | coordinateFlagNotValidForCompression);
                    WritePoint((Point)geom, writeCoordinates, gaiaExport, bw);
                    break;
                case OgcGeometryType.LineString:
                    gaiaExport.WriteInt32(bw, (int)GaiaGeoGeometry.GAIA_LINESTRING | coordinateFlag);
                    WriteLineString((LineString)geom, writeCoordinates, gaiaExport, bw);
                    break;
                case OgcGeometryType.Polygon:
                    gaiaExport.WriteInt32(bw, (int)GaiaGeoGeometry.GAIA_POLYGON | coordinateFlag);
                    WritePolygon((Polygon)geom, writeCoordinates, gaiaExport, bw);
                    break;
                case OgcGeometryType.MultiPoint:
                    gaiaExport.WriteInt32(bw, (int)GaiaGeoGeometry.GAIA_MULTIPOINT | coordinateFlagNotValidForCompression);
                    WriteMultiPoint((MultiPoint)geom, writeCoordinates, gaiaExport, bw);
                    break;
                case OgcGeometryType.MultiLineString:
                    gaiaExport.WriteInt32(bw, (int)GaiaGeoGeometry.GAIA_MULTILINESTRING | coordinateFlag);
                    WriteMultiLineString((MultiLineString)geom, writeCoordinates, gaiaExport, bw);
                    break;
                case OgcGeometryType.MultiPolygon:
                    gaiaExport.WriteInt32(bw, (int)GaiaGeoGeometry.GAIA_MULTIPOLYGON | coordinateFlag);
                    WriteMultiPolygon((MultiPolygon)geom, writeCoordinates, gaiaExport, bw);
                    break;
                case OgcGeometryType.GeometryCollection:
                    gaiaExport.WriteInt32(bw, (int)GaiaGeoGeometry.GAIA_GEOMETRYCOLLECTION | coordinateFlagNotValidForCompression);
                    WriteGeometryCollection((GeometryCollection)geom, gaiaExport, bw);
                    break;
                default:
                    throw new ArgumentException("unknown geometry type");
            }
        }

        private static void WriteGeometryCollection(GeometryCollection geom, GaiaExport gaiaExport, BinaryWriter bw)
        {
            gaiaExport.WriteInt32(bw, geom.NumGeometries);
            for (var i = 0; i < geom.NumGeometries; i++)
            {
                bw.Write((byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY);
                WriteGeometry(geom[i], gaiaExport, bw);
            }
        }

        private static void WriteMultiPolygon(GeometryCollection geom, WriteCoordinates writeCoordinates, GaiaExport gaiaExport, BinaryWriter bw)
        {
            gaiaExport.WriteInt32(bw, geom.NumGeometries);
            for (var i = 0; i < geom.NumGeometries; i++)
            {
                bw.Write((byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY);
                gaiaExport.WriteInt32(bw, gaiaExport.CoordinateFlag | (int)GaiaGeoGeometry.GAIA_POLYGON);
                WritePolygon((Polygon)geom[i], writeCoordinates, gaiaExport, bw);
            }
        }

        private static void WriteMultiLineString(MultiLineString geom, WriteCoordinates writeCoordinates, GaiaExport gaiaExport, BinaryWriter bw)
        {
            gaiaExport.WriteInt32(bw, geom.NumGeometries);
            for (var i = 0; i < geom.NumGeometries; i++)
            {
                bw.Write((byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY);
                gaiaExport.WriteInt32(bw, gaiaExport.CoordinateFlag | (int)GaiaGeoGeometry.GAIA_LINESTRING);
                WriteLineString((LineString)geom[i], writeCoordinates, gaiaExport, bw);
            }
        }

        private static void WriteMultiPoint(MultiPoint geom, WriteCoordinates writeCoordinates, GaiaExport gaiaExport, BinaryWriter bw)
        {
            var wi = gaiaExport.WriteInt32;

            // number of coordinates
            wi(bw, geom.NumGeometries);

            // get the coordinate flag
            var coordinateFlag = gaiaExport.CoordinateFlagUncompressed;

            for (var i = 0; i < geom.NumGeometries; i++)
            {
                //write entity begin marker
                bw.Write((byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY);

                //write entity marker
                wi(bw, coordinateFlag + (int)GaiaGeoGeometryEntity.GAIA_TYPE_POINT);

                //write coordinates
                writeCoordinates(((Point)geom[i]).CoordinateSequence, gaiaExport, bw);
            }
        }

        private static void WritePolygon(Polygon geom, WriteCoordinates writeCoordinates, GaiaExport gaiaExport, BinaryWriter bw)
        {
            gaiaExport.WriteInt32(bw, geom.NumInteriorRings + 1);
            WriteLineString(geom.Shell, writeCoordinates, gaiaExport, bw);
            for (var i = 0; i < geom.NumInteriorRings; i++)
                WriteLineString(geom.GetInteriorRingN(i), writeCoordinates, gaiaExport, bw);
        }

        private static void WriteLineString(LineString geom, WriteCoordinates writeCoordinates, GaiaExport gaiaExport, BinaryWriter bw)
        {
            var seq = geom.CoordinateSequence;
            gaiaExport.WriteInt32(bw, seq.Count);
            writeCoordinates(geom.CoordinateSequence, gaiaExport, bw);
        }

        private static void WritePoint(Point geom, WriteCoordinates writeCoordinates, GaiaExport gaiaExport, BinaryWriter bw)
        {
            writeCoordinates(geom.CoordinateSequence, gaiaExport, bw);
        }

        private static GaiaExport SetGaiaGeoExportFunctions(GaiaGeoEndianMarker gaiaGeoEndianMarker, bool hasZ, bool hasM, bool useCompression)
        {
            var conversionNeeded = false;
            switch (gaiaGeoEndianMarker)
            {
                case GaiaGeoEndianMarker.GAIA_LITTLE_ENDIAN:
                    if (!BitConverter.IsLittleEndian)
                        conversionNeeded = true;
                    break;
                case GaiaGeoEndianMarker.GAIA_BIG_ENDIAN:
                    if (BitConverter.IsLittleEndian)
                        conversionNeeded = true;
                    break;
                default:
                    /* unknown encoding; nor litte-endian neither big-endian */
                    return null;
            }

            var gaiaExport = GaiaExport.Create(conversionNeeded);
            gaiaExport.SetCoordinateType(hasZ, hasM, useCompression);
            return gaiaExport;
        }

        private delegate void WriteCoordinates(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw);

        private static void WriteXY(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;

            for (var i = 0; i < coordinateSequence.Count; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                wd(bw, c.X, c.Y);
            }
        }

        private static void WriteXYZ(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;
            for (var i = 0; i < coordinateSequence.Count; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                wd(bw, c.X, c.Y, c.Z);
            }
        }

        private static void WriteXYM(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;
            for (var i = 0; i < coordinateSequence.Count; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                wd(bw, c.X, c.Y, coordinateSequence.GetOrdinate(i, Ordinate.M));
            }
        }

        private static void WriteXYZM(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;
            for (var i = 0; i < coordinateSequence.Count; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                wd(bw, c.X, c.Y, c.Z, coordinateSequence.GetOrdinate(i, Ordinate.M));
            }
        }

        private static void WriteCompressedXY(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;

            // Write initial coordinate
            var cprev = coordinateSequence.GetCoordinate(0);
            wd(bw, cprev.X, cprev.Y);

            var ws = export.WriteSingle;
            var maxIndex = coordinateSequence.Count - 1;
            if (maxIndex <= 0) return;

            for (var i = 1; i < maxIndex; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                var fx = (float)(c.X - cprev.X);
                var fy = (float)(c.Y - cprev.Y);
                ws(bw, fx, fy);
                cprev = c;
            }

            // Write last coordinate
            cprev = coordinateSequence.GetCoordinate(maxIndex);
            wd(bw, cprev.X, cprev.Y);
        }

        private static void WriteCompressedXYZ(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;

            // Write initial coordinate
            var cprev = coordinateSequence.GetCoordinate(0);
            wd(bw, cprev.X, cprev.Y, cprev.Z);

            var maxIndex = coordinateSequence.Count - 1;
            if (maxIndex <= 0) return;

            var ws = export.WriteSingle;
            for (var i = 1; i < maxIndex; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                var fx = (float)(c.X - cprev.X);
                var fy = (float)(c.Y - cprev.Y);
                var fz = (float)(c.Z - cprev.Z);
                ws(bw, fx, fy, fz);
                cprev = c;
            }
            cprev = coordinateSequence.GetCoordinate(maxIndex);
            wd(bw, cprev.X, cprev.Y, cprev.Z);
        }

        private static void WriteCompressedXYM(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;

            // Write initial coordinate
            var cprev = coordinateSequence.GetCoordinate(0);
            var mprev = coordinateSequence.GetOrdinate(0, Ordinate.M);
            wd(bw, cprev.X, cprev.Y, mprev);

            var maxIndex = coordinateSequence.Count - 1;
            if (maxIndex <= 0) return;

            var ws = export.WriteSingle;
            for (var i = 1; i < maxIndex; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                var fx = (float)(c.X - cprev.X);
                var fy = (float)(c.Y - cprev.Y);
                var fm = (float)(coordinateSequence.GetOrdinate(i, Ordinate.M) - mprev);
                ws(bw, fx, fy, fm);
                cprev = c;
            }
            cprev = coordinateSequence.GetCoordinate(maxIndex);
            mprev = coordinateSequence.GetOrdinate(maxIndex, Ordinate.M);
            wd(bw, cprev.X, cprev.Y, mprev);
        }

        private static void WriteCompressedXYZM(CoordinateSequence coordinateSequence, GaiaExport export, BinaryWriter bw)
        {
            var wd = export.WriteDouble;

            // Write initial coordinate
            var cprev = coordinateSequence.GetCoordinate(0);
            var mprev = coordinateSequence.GetOrdinate(0, Ordinate.M);
            wd(bw, cprev.X, cprev.Y, cprev.Z, mprev);

            var maxIndex = coordinateSequence.Count - 1;
            if (maxIndex <= 0) return;

            var ws = export.WriteSingle;
            for (var i = 1; i < maxIndex; i++)
            {
                var c = coordinateSequence.GetCoordinate(i);
                var fx = (float)(c.X - cprev.X);
                var fy = (float)(c.Y - cprev.Y);
                var fz = (float)(c.Z - cprev.Z);
                var fm = (float)(coordinateSequence.GetOrdinate(i, Ordinate.M) - mprev);
                ws(bw, fx, fy, fz, fm);
                cprev = c;
            }
            cprev = coordinateSequence.GetCoordinate(maxIndex);
            mprev = coordinateSequence.GetOrdinate(maxIndex, Ordinate.M);
            wd(bw, cprev.X, cprev.Y, cprev.Z, mprev);
        }
    }
}
