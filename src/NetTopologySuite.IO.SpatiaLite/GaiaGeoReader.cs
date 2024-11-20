// Copyright (c) Felix Obermaier (ivv-aachen.de) and the NetTopologySuite Team
// Licensed under the BSD 3-Clause license. See LICENSE.md in the project root for license information.

using System;
using System.IO;
using NetTopologySuite.Geometries;

namespace NetTopologySuite.IO
{
    /// <summary>
    /// Class to read SpatiaLite geometries from an array of bytes
    /// </summary>
    public class GaiaGeoReader
    {
        private GeometryFactory _factory;
        private readonly PrecisionModel _precisionModel;
        private readonly CoordinateSequenceFactory _coordinateSequenceFactory;
        private Ordinates _handleOrdinates;

        /// <summary>
        /// Creates an instance of this class using the default <see cref="CoordinateSequenceFactory"/> and <see cref="PrecisionModel"/> to use.
        /// </summary>
        public GaiaGeoReader()
            : this(NtsGeometryServices.Instance.DefaultCoordinateSequenceFactory, NtsGeometryServices.Instance.DefaultPrecisionModel)
        { }

        /// <summary>
        /// Creates an instance of this class using the provided <see cref="CoordinateSequenceFactory"/> and <see cref="PrecisionModel"/> to use.
        /// </summary>
        public GaiaGeoReader(CoordinateSequenceFactory coordinateSequenceFactory, PrecisionModel precisionModel)
            : this(coordinateSequenceFactory, precisionModel, Ordinates.XYZM)
        {
        }

        /// <summary>
        /// Creates an instance of this class using the provided <see cref="CoordinateSequenceFactory"/> and <see cref="PrecisionModel"/> to use.
        /// Additionally the ordinate values that are to be handled can be set.
        /// </summary>
        public GaiaGeoReader(CoordinateSequenceFactory coordinateSequenceFactory, PrecisionModel precisionModel, Ordinates handleOrdinates)
        {
            _coordinateSequenceFactory = coordinateSequenceFactory;
            _precisionModel = precisionModel;
            _handleOrdinates = handleOrdinates;
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

            if (blob.Length < 45)
                return null;		/* cannot be an internal BLOB WKB geometry */
            if ((GaiaGeoBlobMark)blob[0] != GaiaGeoBlobMark.GAIA_MARK_START)
                return null;		/* failed to recognize START signature */
            var size = blob.Length;
            if ((GaiaGeoBlobMark)blob[size - 1] != GaiaGeoBlobMark.GAIA_MARK_END)
                return null;		/* failed to recognize END signature */
            if ((GaiaGeoBlobMark)blob[38] != GaiaGeoBlobMark.GAIA_MARK_MBR)
                return null;		/* failed to recognize MBR signature */

            var gaiaImport = SetGaiaGeoParseFunctions((GaiaGeoEndianMarker)blob[1], HandleOrdinates);
            if (gaiaImport == null)
                return null;

            //geo = gaiaAllocGeomColl();
            var offset = 2;
            var srid = gaiaImport.GetInt32(blob, ref offset);

            if (_factory == null || _factory.SRID != srid)
                _factory = NtsGeometryServices.Instance.CreateGeometryFactory(_precisionModel, srid,
                                                                              _coordinateSequenceFactory);
            var factory = _factory;

            //geo->endian_arch = (char)endian_arch;
            //geo->endian = (char)little_endian;
            //geo->blob = blob;
            //geo->size = size;
            //offset = 43;
            //switch ((GaiaGeoGeometry)type)
            //{
            //    /* setting up DimensionModel */
            //    case GaiaGeoGeometry.GAIA_POINTZ:
            //    case GaiaGeoGeometry.GAIA_LINESTRINGZ:
            //    case GaiaGeoGeometry.GAIA_POLYGONZ:
            //    case GaiaGeoGeometry.GAIA_MULTIPOINTZ:
            //    case GaiaGeoGeometry.GAIA_MULTILINESTRINGZ:
            //    case GaiaGeoGeometry.GAIA_MULTIPOLYGONZ:
            //    case GaiaGeoGeometry.GAIA_GEOMETRYCOLLECTIONZ:
            //    case GaiaGeoGeometry.GAIA_COMPRESSED_LINESTRINGZ:
            //    case GaiaGeoGeometry.GAIA_COMPRESSED_POLYGONZ:
            //        geo->DimensionModel = GAIA_XY_Z;
            //        break;
            //    case GaiaGeoGeometry.GAIA_POINTM:
            //    case GaiaGeoGeometry.GAIA_LINESTRINGM:
            //    case GaiaGeoGeometry.GAIA_POLYGONM:
            //    case GaiaGeoGeometry.GAIA_MULTIPOINTM:
            //    case GaiaGeoGeometry.GAIA_MULTILINESTRINGM:
            //    case GaiaGeoGeometry.GAIA_MULTIPOLYGONM:
            //    case GaiaGeoGeometry.GAIA_GEOMETRYCOLLECTIONM:
            //    case GaiaGeoGeometry.GAIA_COMPRESSED_LINESTRINGM:
            //    case GaiaGeoGeometry.GAIA_COMPRESSED_POLYGONM:
            //        geo->DimensionModel = GAIA_XY_M;
            //        break;
            //    case GaiaGeoGeometry.GAIA_POINTZM:
            //    case GaiaGeoGeometry.GAIA_LINESTRINGZM:
            //    case GaiaGeoGeometry.GAIA_POLYGONZM:
            //    case GaiaGeoGeometry.GAIA_MULTIPOINTZM:
            //    case GaiaGeoGeometry.GAIA_MULTILINESTRINGZM:
            //    case GaiaGeoGeometry.GAIA_MULTIPOLYGONZM:
            //    case GaiaGeoGeometry.GAIA_GEOMETRYCOLLECTIONZM:
            //    case GaiaGeoGeometry.GAIA_COMPRESSED_LINESTRINGZM:
            //    case GaiaGeoGeometry.GAIA_COMPRESSED_POLYGONZM:
            //        geo->DimensionModel = GAIA_XY_Z_M;
            //        break;
            //    default:
            //        geo->DimensionModel = GAIA_XY;
            //        break;
            //};
            offset = 6;

            //the next four values are the envelope, but it is not required here
            //consider that if the values contain double.NaN
            //the NetTopologySuite.Geometries.Envelope constructor might fail

            for (int i = 0; i != 4; ++i)
            {
                gaiaImport.GetDouble(blob, ref offset);
            }

            offset = 39;
            var type = (GaiaGeoGeometry)gaiaImport.GetInt32(blob, ref offset);
            var geom = ParseWkbGeometry(type, blob, ref offset, factory, gaiaImport);
            if (geom != null)
            {
                geom.SRID = srid;
                //geom.Envelope = env;
            }
            return geom;
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

            var buffer = new byte[stream.Length];
            stream.Read(buffer, 0, buffer.Length);
            return Read(buffer);
        }

        private static ReadCoordinatesFunction SetReadCoordinatesFunction(GaiaImport gaiaImport, GaiaGeoGeometry type)
        {
            gaiaImport.SetGeometryType(type);

            if (gaiaImport.Uncompressed)
            {
                if (gaiaImport.HasZ && gaiaImport.HasM)
                    return ReadXYZM;
                if (gaiaImport.HasM)
                    return ReadXYM;
                if (gaiaImport.HasZ)
                    return ReadXYZ;
                return ReadXY;
            }

            if (gaiaImport.HasZ && gaiaImport.HasM)
                return ReadCompressedXYZM;
            if (gaiaImport.HasM)
                return ReadCompressedXYM;
            if (gaiaImport.HasZ)
                return ReadCompressedXYZ;
            return ReadCompressedXY;
        }

        private static GaiaGeoGeometry ToBaseGeometryType(GaiaGeoGeometry geometry)
        {
            var geometryInt = (int)geometry;
            if (geometryInt > 1000000) geometryInt -= 1000000;
            if (geometryInt > 3000) geometryInt -= 3000;
            if (geometryInt > 2000) geometryInt -= 2000;
            if (geometryInt > 1000) geometryInt -= 1000;
            return (GaiaGeoGeometry)geometryInt;
        }

        private static Geometry ParseWkbGeometry(GaiaGeoGeometry type, byte[] blob, ref int offset, GeometryFactory factory, GaiaImport gaiaImport)
        {
            var readCoordinates = SetReadCoordinatesFunction(gaiaImport, type);

            switch (ToBaseGeometryType(type))
            {
                case GaiaGeoGeometry.GAIA_POINT:
                    return ParseWkbPoint(blob, ref offset, factory, readCoordinates, gaiaImport);

                case GaiaGeoGeometry.GAIA_MULTIPOINT:
                    return ParseWkbMultiPoint(blob, ref offset, factory, readCoordinates, gaiaImport);

                case GaiaGeoGeometry.GAIA_LINESTRING:
                    return ParseWkbLineString(blob, ref offset, factory, readCoordinates, gaiaImport);

                case GaiaGeoGeometry.GAIA_MULTILINESTRING:
                    return ParseWkbMultiLineString(blob, ref offset, factory, readCoordinates, gaiaImport);

                case GaiaGeoGeometry.GAIA_POLYGON:
                    return ParseWkbPolygon(blob, ref offset, factory, readCoordinates, gaiaImport);

                case GaiaGeoGeometry.GAIA_MULTIPOLYGON:
                    return ParseWkbMultiPolygon(blob, ref offset, factory, readCoordinates, gaiaImport);

                case GaiaGeoGeometry.GAIA_GEOMETRYCOLLECTION:
                    return ParseWkbGeometryCollection(blob, ref offset, factory, gaiaImport);
            }
            return null;
        }

        private static GaiaImport SetGaiaGeoParseFunctions(GaiaGeoEndianMarker gaiaGeoEndianMarker, Ordinates handleOrdinates)
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
                    throw new ArgumentOutOfRangeException("gaiaGeoEndianMarker");
            }

            return GaiaImport.Create(conversionNeeded, handleOrdinates);
        }

        private static Point ParseWkbPoint(byte[] blob, ref int offset, GeometryFactory factory, ReadCoordinatesFunction readCoordinates, GaiaImport gaiaImport)
        {
            return factory.CreatePoint(readCoordinates(blob, ref offset, 1, gaiaImport, factory.CoordinateSequenceFactory, factory.PrecisionModel));
        }

        private static MultiPoint ParseWkbMultiPoint(byte[] blob, ref int offset, GeometryFactory factory, ReadCoordinatesFunction readCoordinates, GaiaImport gaiaImport)
        {
            var getInt32 = gaiaImport.GetInt32;
            var getDouble = gaiaImport.GetDouble;

            var number = getInt32(blob, ref offset);

            int measures = gaiaImport.HasM ? 1 : 0;
            int dimension = 2 + (gaiaImport.HasZ ? 1 : 0) + measures;
            var coordTemplate = Coordinates.Create(dimension, measures);

            var coords = new Coordinate[number];
            for (int i = 0; i < coords.Length; i++)
            {
                coords[i] = coordTemplate.Copy();
            }

            for (var i = 0; i < number; i++)
            {
                if (blob[offset++] != (byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY)
                    throw new Exception();

                var gt = getInt32(blob, ref offset);
                if (ToBaseGeometryType((GaiaGeoGeometry)gt) != GaiaGeoGeometry.GAIA_POINT)
                    throw new Exception();

                coords[i].X = getDouble(blob, ref offset);
                coords[i].Y = getDouble(blob, ref offset);
                if (gaiaImport.HasZ)
                    coords[i].Z = getDouble(blob, ref offset);
                if (gaiaImport.HasM)
                    coords[i].M = getDouble(blob, ref offset);
            }
            return factory.CreateMultiPointFromCoords(coords);
        }

        private delegate LineString CreateLineStringFunction(CoordinateSequence coordinates);

        private static LineString ParseWkbLineString(byte[] blob, ref int offset, GeometryFactory factory, ReadCoordinatesFunction readCoordinates, GaiaImport gaiaImport)
        {
            return ParseWkbLineString(blob, ref offset, factory, factory.CreateLineString, readCoordinates,
                                      gaiaImport);
        }

        private static LineString ParseWkbLineString(byte[] blob, ref int offset, GeometryFactory factory, CreateLineStringFunction createLineStringFunction, ReadCoordinatesFunction readCoordinates, GaiaImport gaiaImport)
        {
            var number = gaiaImport.GetInt32(blob, ref offset);
            var sequence = readCoordinates(blob, ref offset, number, gaiaImport, factory.CoordinateSequenceFactory,
                                           factory.PrecisionModel);
            return createLineStringFunction(sequence);
        }

        private static MultiLineString ParseWkbMultiLineString(byte[] blob, ref int offset, GeometryFactory factory, ReadCoordinatesFunction readCoordinates, GaiaImport gaiaImport)
        {
            int number = gaiaImport.GetInt32(blob, ref offset);
            var lineStrings = new LineString[number];
            for (var i = 0; i < number; i++)
            {
                if (blob[offset++] != (byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY)
                    throw new Exception();

                var gt = gaiaImport.GetInt32(blob, ref offset);
                if (ToBaseGeometryType((GaiaGeoGeometry)gt) != GaiaGeoGeometry.GAIA_LINESTRING)
                    throw new Exception();

                //Since Uncompressed MultiGeom can contain compressed we need to set it here also
                readCoordinates = SetReadCoordinatesFunction(gaiaImport, (GaiaGeoGeometry)gt);

                lineStrings[i] = ParseWkbLineString(blob, ref offset, factory, factory.CreateLineString, readCoordinates, gaiaImport);
            }
            return factory.CreateMultiLineString(lineStrings);
        }

        private static Polygon ParseWkbPolygon(byte[] blob, ref int offset, GeometryFactory factory, ReadCoordinatesFunction readCoordinates, GaiaImport gaiaImport)
        {
            var number = gaiaImport.GetInt32(blob, ref offset) - 1;
            var shell = (LinearRing)ParseWkbLineString(blob, ref offset, factory, factory.CreateLinearRing, readCoordinates, gaiaImport);
            var holes = new LinearRing[number];
            for (var i = 0; i < number; i++)
                holes[i] = (LinearRing)ParseWkbLineString(blob, ref offset, factory, factory.CreateLinearRing, readCoordinates, gaiaImport);

            return factory.CreatePolygon(shell, holes);
        }

        private static Geometry ParseWkbMultiPolygon(byte[] blob, ref int offset, GeometryFactory factory, ReadCoordinatesFunction readCoordinates, GaiaImport gaiaImport)
        {
            var number = gaiaImport.GetInt32(blob, ref offset);
            var polygons = new Polygon[number];
            for (var i = 0; i < number; i++)
            {
                if (blob[offset++] != (byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY)
                    throw new Exception();

                var gt = gaiaImport.GetInt32(blob, ref offset);
                if (ToBaseGeometryType((GaiaGeoGeometry)gt) != GaiaGeoGeometry.GAIA_POLYGON)
                    throw new Exception();

                //Since Uncompressed MultiGeom can contain compressed we need to set it here also
                readCoordinates = SetReadCoordinatesFunction(gaiaImport, (GaiaGeoGeometry)gt);


                polygons[i] = ParseWkbPolygon(blob, ref offset, factory, readCoordinates, gaiaImport);
            }
            return factory.CreateMultiPolygon(polygons);
        }

        private static GeometryCollection ParseWkbGeometryCollection(byte[] blob, ref int offset, GeometryFactory factory, GaiaImport gaiaImport)
        {
            var number = gaiaImport.GetInt32(blob, ref offset);
            var geometries = new Geometry[number];
            for (var i = 0; i < number; i++)
            {
                if (blob[offset++] != (byte)GaiaGeoBlobMark.GAIA_MARK_ENTITY)
                    throw new Exception();

                geometries[i] = ParseWkbGeometry((GaiaGeoGeometry)gaiaImport.GetInt32(blob, ref offset), blob, ref offset, factory, gaiaImport);
            }
            return factory.CreateGeometryCollection(geometries);
        }

        private delegate CoordinateSequence ReadCoordinatesFunction(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel);

        private static CoordinateSequence ReadXY(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var ordinateValues = import.GetDoubles(buffer, ref offset, number * 2);
            var ret = factory.Create(number, Ordinates.XY);
            var j = 0;
            for (var i = 0; i < number; i++)
            {
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(ordinateValues[j++]));
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(ordinateValues[j++]));
            }
            return ret;
        }

        private static CoordinateSequence ReadXYZ(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var ordinateValues = import.GetDoubles(buffer, ref offset, number * 3);
            var ret = factory.Create(number, import.HandleOrdinates);
            var handleZ = (ret.Ordinates & Ordinates.Z) == Ordinates.Z;
            var j = 0;
            for (var i = 0; i < number; i++)
            {
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(ordinateValues[j++]));
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(ordinateValues[j++]));
                if (handleZ) ret.SetOrdinate(i, Ordinate.Z, precisionModel.MakePrecise(ordinateValues[j]));
                j++;
            }
            return ret;
        }

        private static CoordinateSequence ReadXYM(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var ordinateValues = import.GetDoubles(buffer, ref offset, number * 3);
            var ret = factory.Create(number, import.HandleOrdinates);
            var handleM = (ret.Ordinates & Ordinates.M) == Ordinates.M;
            var j = 0;
            for (var i = 0; i < number; i++)
            {
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(ordinateValues[j++]));
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(ordinateValues[j++]));
                if (handleM) ret.SetOrdinate(i, Ordinate.M, precisionModel.MakePrecise(ordinateValues[j]));
                j++;
            }
            return ret;
        }

        private static CoordinateSequence ReadXYZM(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var ordinateValues = import.GetDoubles(buffer, ref offset, number * 4);
            var ret = factory.Create(number, import.HandleOrdinates);
            var handleZ = (ret.Ordinates & Ordinates.Z) == Ordinates.Z;
            var handleM = (ret.Ordinates & Ordinates.M) == Ordinates.M;
            var j = 0;
            for (var i = 0; i < number; i++)
            {
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(ordinateValues[j++]));
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(ordinateValues[j++]));
                if (handleZ) ret.SetOrdinate(i, Ordinate.Z, precisionModel.MakePrecise(ordinateValues[j]));
                j++;
                if (handleM) ret.SetOrdinate(i, Ordinate.M, precisionModel.MakePrecise(ordinateValues[j]));
                j++;
            }
            return ret;
        }

        private static CoordinateSequence ReadCompressedXY(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var startOrdinateValues = import.GetDoubles(buffer, ref offset, 2);
            var ret = factory.Create(number, import.HandleOrdinates);

            var x = startOrdinateValues[0];
            var y = startOrdinateValues[1];
            ret.SetOrdinate(0, Ordinate.X, precisionModel.MakePrecise(x));
            ret.SetOrdinate(0, Ordinate.Y, precisionModel.MakePrecise(y));

            if (number == 1) return ret;

            var ordinateValues = import.GetSingles(buffer, ref offset, (number - 2) * 2);

            var j = 0;
            int i;
            for (i = 1; i < number - 1; i++)
            {
                x = x + ordinateValues[j++];
                y = y + ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(x));
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(y));
            }

            startOrdinateValues = import.GetDoubles(buffer, ref offset, 2);
            ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(startOrdinateValues[0]));
            ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(startOrdinateValues[1]));

            return ret;
        }

        private static CoordinateSequence ReadCompressedXYZ(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var startOrdinateValues = import.GetDoubles(buffer, ref offset, 3);
            var ret = factory.Create(number, Ordinates.XYZ);

            var handleZ = (ret.Ordinates & Ordinates.Z) == Ordinates.Z;

            var x = startOrdinateValues[0];
            ret.SetOrdinate(0, Ordinate.X, precisionModel.MakePrecise(x));
            var y = startOrdinateValues[1];
            ret.SetOrdinate(0, Ordinate.Y, precisionModel.MakePrecise(y));
            var z = handleZ ? startOrdinateValues[2] : Coordinate.NullOrdinate;
            ret.SetOrdinate(0, Ordinate.Z, z);

            if (number == 1) return ret;

            var ordinateValues = import.GetSingles(buffer, ref offset, (number - 2) * 3);

            var j = 0;
            int i;
            for (i = 1; i < number - 1; i++)
            {
                x += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(x));
                y += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(y));
                if (handleZ) z += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.Z, z);
            }

            startOrdinateValues = import.GetDoubles(buffer, ref offset, 3);
            ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(startOrdinateValues[0]));
            ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(startOrdinateValues[1]));
            z = handleZ ? startOrdinateValues[2] : Coordinate.NullOrdinate;
            ret.SetOrdinate(i, Ordinate.Z, z);
            return ret;
        }

        private static CoordinateSequence ReadCompressedXYM(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var startOrdinateValues = import.GetDoubles(buffer, ref offset, 3);
            var ret = factory.Create(number, Ordinates.XYM);

            var handleM = (ret.Ordinates & Ordinates.M) == Ordinates.M;

            var x = startOrdinateValues[0];
            ret.SetOrdinate(0, Ordinate.X, precisionModel.MakePrecise(x));
            var y = startOrdinateValues[1];
            ret.SetOrdinate(0, Ordinate.Y, precisionModel.MakePrecise(y));
            var m = handleM ? startOrdinateValues[2] : Coordinate.NullOrdinate;
            ret.SetOrdinate(0, Ordinate.M, m);

            if (number == 1) return ret;

            var ordinateValues = import.GetSingles(buffer, ref offset, (number - 2) * 3);

            var j = 0;
            int i;
            for (i = 1; i < number - 1; i++)
            {
                x += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(x));
                y += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(y));
                if (handleM) m += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.M, m);
            }

            startOrdinateValues = import.GetDoubles(buffer, ref offset, 3);
            ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(startOrdinateValues[0]));
            ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(startOrdinateValues[1]));
            m = handleM ? startOrdinateValues[2] : Coordinate.NullOrdinate;
            ret.SetOrdinate(i, Ordinate.M, m);
            return ret;
        }

        private static CoordinateSequence ReadCompressedXYZM(byte[] buffer, ref int offset, int number, GaiaImport import, CoordinateSequenceFactory factory, PrecisionModel precisionModel)
        {
            var startOrdinateValues = import.GetDoubles(buffer, ref offset, 4);
            var ret = factory.Create(number, Ordinates.XYM);

            var handleZ = (ret.Ordinates & Ordinates.Z) == Ordinates.Z;
            var handleM = (ret.Ordinates & Ordinates.M) == Ordinates.M;

            var x = startOrdinateValues[0];
            ret.SetOrdinate(0, Ordinate.X, precisionModel.MakePrecise(x));
            var y = startOrdinateValues[1];
            ret.SetOrdinate(0, Ordinate.Y, precisionModel.MakePrecise(y));
            var z = handleZ ? startOrdinateValues[2] : Coordinate.NullOrdinate;
            ret.SetOrdinate(0, Ordinate.Z, z);
            var m = handleM ? startOrdinateValues[3] : Coordinate.NullOrdinate;
            ret.SetOrdinate(0, Ordinate.M, m);

            if (number == 1) return ret;

            var ordinateValues = import.GetSingles(buffer, ref offset, (number - 2) * 4);

            var j = 0;
            int i;
            for (i = 1; i < number - 1; i++)
            {
                x += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(x));
                y += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(y));
                if (handleZ) z += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.Z, z);
                if (handleM) m += ordinateValues[j++];
                ret.SetOrdinate(i, Ordinate.M, m);
            }

            startOrdinateValues = import.GetDoubles(buffer, ref offset, 4);
            ret.SetOrdinate(i, Ordinate.X, precisionModel.MakePrecise(startOrdinateValues[0]));
            ret.SetOrdinate(i, Ordinate.Y, precisionModel.MakePrecise(startOrdinateValues[1]));
            z = handleZ ? startOrdinateValues[2] : Coordinate.NullOrdinate;
            ret.SetOrdinate(i, Ordinate.Z, z);
            m = handleM ? startOrdinateValues[3] : Coordinate.NullOrdinate;
            ret.SetOrdinate(i, Ordinate.M, m);
            return ret;
        }
    }
}
