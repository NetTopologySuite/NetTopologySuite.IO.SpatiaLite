using System;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;

namespace NetTopologySuite.IO.SpatiaLite.Test
{
    public class RandomGeometryHelper
    {
        public static readonly Random RND = new Random(9987462);

        private bool _hasZ;

        private bool _hasM;

        public RandomGeometryHelper(GeometryFactory factory)
        {
            Factory = factory;
            //_geometricShapeFactory = new SineStarFactory(factory);
            MinX = -180;
            MaxX = 180;
            MinY = -90;
            MaxY = 90;
        }

        public Ordinates Ordinates
        {
            get => Ordinates.XY | (_hasZ ? Ordinates.Z : Ordinates.None) | (_hasM ? Ordinates.M : Ordinates.None);
            set => (_hasZ, _hasM) = (value.HasFlag(Ordinates.Z), value.HasFlag(Ordinates.M));
        }

        public double MinX { get; set; }

        public double MaxX { get; set; }

        public double MinY { get; set; }

        public double MaxY { get; set; }

        /// <summary>
        /// Gets a random coordinate
        /// </summary>
        protected Coordinate RandomCoordinate
        {
            get
            {
                var c = Coordinates.Create(2 + (_hasZ ? 1 : 0) + (_hasM ? 1 : 0), _hasM ? 1 : 0);
                c.X = RandomOrdinate(Ordinate.X);
                c.Y = RandomOrdinate(Ordinate.Y);
                if (_hasZ)
                    c.Z = RandomOrdinate(Ordinate.Z);
                if (_hasM)
                    c.M = RandomOrdinate(Ordinate.M);

                return c;
            }
        }

        protected Coordinate[] RandomCoordinates
        {
            get
            {
                var pts = new Coordinate[RND.Next(4, 15)];
                for (var i = 0; i < pts.Length; i++)
                {
                    pts[i] = RandomCoordinate;
                    _factory.PrecisionModel.MakePrecise(pts[i]);
                }

                return pts;
            }
        }

        private GeometryFactory _factory;

        public int SRID
        {
            get { return Factory.SRID; }
            set
            {
                var pm = Factory.PrecisionModel;
                Factory = new GeometryFactory(pm, value);
            }
        }

        /// <summary>
        /// Gets the factory to create the random geometries
        /// </summary>
        public GeometryFactory Factory
        {
            get { return _factory; }
            set
            {
                if (value != null)
                {
                    _factory = value;
                    _geometricShapeFactory = new SineStarFactory(_factory);
                }
            }
        }

        /// <summary>
        /// Gets a point with random ordinates
        /// </summary>
        public Point Point { get { return Factory.CreatePoint(RandomCoordinate); } }

        /// <summary>
        /// Gets a multipoint of a random number of points with random ordinates
        /// </summary>
        public MultiPoint MultiPoint
        {
            get
            {
                return Factory.CreateMultiPointFromCoords(RandomCoordinates);
            }
        }

        public LineString LineString
        {
            get { return Factory.CreateLineString(RandomCoordinates); }
        }

        public MultiLineString MultiLineString
        {
            get
            {
                var lineStrings = new LineString[RND.Next(5, 9)];
                for (var i = 0; i < lineStrings.Length; i++)
                    lineStrings[i] = LineString;
                return Factory.CreateMultiLineString(lineStrings);
            }
        }

        private SineStarFactory _geometricShapeFactory;

        public Polygon Polygon
        {
            get
            {
                var result = GetResult();
                if (Ordinates == Ordinates.XY)
                {
                    return result;
                }

                // the result doesn't have Z / M, but we need it.  add it now.
                var ed = new GeometryEditor(Factory);
                return (Polygon)ed.Edit(result, new OrdinateZMFiller(this));
                Polygon GetResult()
                {
                    lock (_geometricShapeFactory)
                    {
                        _geometricShapeFactory.Centre = RandomCoordinate;
                        _geometricShapeFactory.Height = 40 * RND.NextDouble();
                        _geometricShapeFactory.Width = 40 * RND.NextDouble();
                        switch (RND.Next(0, 6))
                        {
                            case 0:
                                return _geometricShapeFactory.CreateArcPolygon(2 * Math.PI * RND.NextDouble(),
                                                                               2 * Math.PI * RND.NextDouble());
                            default:
                                var poly = _geometricShapeFactory.CreateCircle();
                                var distance = -0.25 * Math.Min(_geometricShapeFactory.Height, _geometricShapeFactory.Width);
                                var buffer = (Polygon)poly.Buffer(distance);
                                return _factory.CreatePolygon(poly.Shell, new[] { buffer.Shell });

                            case 2:
                                return _geometricShapeFactory.CreateRectangle();
                            case 3:
                                //return _geometricShapeFactory.CreateSineStar();
                                return _geometricShapeFactory.CreateSquircle();
                            case 4:
                                return _geometricShapeFactory.CreateSupercircle(RND.NextDouble() * 3);
                        }
                    }
                }
            }
        }

        public MultiPolygon MultiPolygon
        {
            get
            {
                var polys = new Polygon[RND.Next(3, 8)];
                for (var i = 0; i < polys.Length; i++)
                    polys[i] = Polygon;
                var mp = Factory.CreateMultiPolygon(polys);
                var mpUnion = mp.Union();
                var multiPolygon = mpUnion as MultiPolygon;
                var result = multiPolygon ?? Factory.CreateMultiPolygon(new[] { (Polygon)mpUnion });
                if (Ordinates == Ordinates.XY)
                {
                    return result;
                }

                // the result doesn't have Z / M, but we need it.  add it now.
                var ed = new GeometryEditor(Factory);
                return (MultiPolygon)ed.Edit(result, new OrdinateZMFiller(this));
            }
        }

        public GeometryCollection GeometryCollection
        {
            get
            {
                var polys = new Geometry[RND.Next(3, 8)];
                for (var i = 0; i < polys.Length; i++)
                    polys[i] = Geometry;
                return Factory.CreateGeometryCollection(polys);
            }
        }

        public Geometry Geometry
        {
            get
            {
                switch (RND.Next(0, 7))
                {
                    case 0:
                        return Point;
                    case 1:
                        return LineString;
                    case 2:
                        return Polygon;
                    case 3:
                        return MultiPoint;
                    case 4:
                        return MultiLineString;
                    case 5:
                        return MultiPolygon;
                    case 6:
                        return GeometryCollection;
                    default:
                        return Geometry;
                }
            }
        }

        /// <summary>
        /// Delegate function to create Coordinate instances. This is particulary handy for m-ordinate coordinate classes
        /// </summary>
        /// <returns></returns>
        protected delegate Coordinate CreateCoordinateDelegate();

        /// <summary>
        /// Function to create a random ordinates. For x-ordinates the values are limited to <see cref="MinX"/> and <see cref="MaxX"/>. For y-Ordinates the bounds are <see cref="MinY"/> and <see cref="MaxY"/>. z- and m-ordinates are limited to the range [0; 1000[.
        /// </summary>
        /// <param name="ordinate">The ordinate to obtain</param>
        /// <returns>A random value that should represent an ordinate value.</returns>
        private double RandomOrdinate(Ordinate ordinate)
        {
            switch (ordinate)
            {
                case Ordinate.X:
                    return MinX + RND.NextDouble() * (MaxX - MinX);
                case Ordinate.Y:
                    return MinY + RND.NextDouble() * (MaxY - MinY);
                default:
                    return RND.NextDouble() * 1000;
            }
        }

        private sealed class OrdinateZMFiller : GeometryEditor.CoordinateSequenceOperation
        {
            public OrdinateZMFiller(RandomGeometryHelper parent)
            {
                EditSequence = Edit;
                CoordinateSequence Edit(CoordinateSequence oldSeq, Geometry g)
                {
                    if (oldSeq is null)
                    {
                        return null;
                    }

                    // don't use RND: that random seed appears to have been specifically chosen to
                    // make our tests run relatively quickly with decently OK coverage... if this
                    // changes to use RND.NextDouble() (or parent.RandomOrdinate), then one specific
                    // test in this suite takes like 4x longer to run than the entire rest of the
                    // test suite itself (airbreather 2021-01-25).
                    var rnd2 = new Random(8675309);
                    var newSeq = g.Factory.CoordinateSequenceFactory.Create(oldSeq.Count, parent.Ordinates);
                    for (int i = 0; i < oldSeq.Count; i++)
                    {
                        newSeq.SetX(i, oldSeq.GetX(i));
                        newSeq.SetY(i, oldSeq.GetY(i));
                        if (parent._hasZ)
                        {
                            newSeq.SetZ(i, rnd2.NextDouble() * 1000);
                        }

                        if (parent._hasM)
                        {
                            newSeq.SetM(i, rnd2.NextDouble() * 1000);
                        }
                    }

                    return newSeq;
                }
            }
        }
    }
}
