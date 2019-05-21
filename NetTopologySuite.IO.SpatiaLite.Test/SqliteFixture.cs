using GeoAPI.Geometries;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NUnit.Framework;
using System;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;

namespace NetTopologySuite.IO.SpatiaLite.Test
{
    [TestFixture]
    [Category("Database.IO")]
    public class SqliteFixture
    {
        [SetUp]
        public virtual void OnFixtureSetUp()
        {
            GeoAPI.GeometryServiceProvider.Instance = NtsGeometryServices.Instance;

            bool is64bit = Environment.Is64BitOperatingSystem && Environment.Is64BitProcess;
            string spatialiteRelPath =
                $"../../../../mod_spatialite/runtimes/{(is64bit ? "win-x64" : "win-x86")}/native";
            string spatialiteFullPath = Path.GetFullPath(spatialiteRelPath);
            Assert.IsTrue(Directory.Exists(spatialiteFullPath), $"path not found: {spatialiteFullPath}");

            string path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process);
            if (path.IndexOf(is64bit
                ? "win-x64"
                : "win-x86", StringComparison.OrdinalIgnoreCase) == -1)
            {
                path = $"{path};{spatialiteFullPath};";
                Environment.SetEnvironmentVariable("Path", path, EnvironmentVariableTarget.Process);
                Debug.WriteLine("'mod_spatialite' libs added to env.path");
            }
            else
            {
                Debug.WriteLine("'mod_spatialite' libs already found in env.path");
            }
        }

        [TearDown]
        public virtual void OnFixtureTearDown() { }

        private static void DoTest(Action<SQLiteConnection> action)
        {
            Assert.IsNotNull(action);

            string fileName = $"{Guid.NewGuid().ToString()}.gpkg";
            File.Copy("empty.gpkg", fileName);
            string cs = $"Data Source={fileName};Version=3;";
            using (var conn = new SQLiteConnection(cs))
            {
                conn.Open();

                conn.EnableExtensions(true);
                conn.LoadExtension("mod_spatialite");
                Debug.WriteLine("'mod_spatialite' extension loaded");

                action(conn);
            }
        }

        [Test]
        public virtual void Existing_point_should_be_read()
        {
            var coord = new Coordinate(11.11, 22.22);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table];
INSERT INTO [sample_feature_table] ([id], [geometry])
VALUES (1, gpkgMakePoint(@px, @py, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", coord.X);
                    cmd.Parameters.AddWithValue("py", coord.Y);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);

                        var gpkgReader = new GeoPackageGeoReader();
                        var geom = gpkgReader.Read(blob);
                        Assert.IsNotNull(geom);
                        Assert.IsInstanceOf<IPoint>(geom);
                        Assert.AreEqual(point, geom);
                        Assert.IsTrue(point.EqualsExact(geom));
                        Assert.AreEqual(coord, geom.Coordinate);
                        Assert.IsTrue(coord.Equals(geom.Coordinate));
                        Assert.IsNaN(geom.Coordinate.Z);
                    }
                }
            });
        }

        /// <summary>
        /// NOTE: this code fails if we configure WkbWriter with handleSRID = Ttrue
        /// see GeoPackageGeoWriter.cs => l.109 
        /// </summary>
        [Test]
        public virtual void New_point_should_be_written()
        {
            var coord = new Coordinate(11.11, 22.22);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table];
INSERT INTO [sample_feature_table] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XY
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());

                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);
                        var gpkgReader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XY
                        };
                        var geom = gpkgReader.Read(blob);
                        Assert.AreEqual(point, geom);
                        Assert.AreEqual(point.AsText(), geom.AsText());

                        Assert.IsFalse(reader.GetBoolean(1));
                        Assert.IsFalse(reader.GetBoolean(2));
                        string wkt = reader.GetString(3);
                        Assert.IsFalse(string.IsNullOrEmpty(wkt));
                        string astext = point.AsText().Replace("POINT (", "POINT(");
                        Assert.AreEqual(astext, wkt);
                    }
                }
            });
        }

        [Test]
        public virtual void Existing_pointZ_should_be_read()
        {
            var coord = new Coordinate(11.11, 22.22, 33.33);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_z];
INSERT INTO [sample_feature_table_z] ([id], [geometry])
VALUES (1, gpkgMakePointZ(@px, @py, @pz, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", coord.X);
                    cmd.Parameters.AddWithValue("py", coord.Y);
                    cmd.Parameters.AddWithValue("pz", coord.Z);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table_z]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);

                        var gpkgReader = new GeoPackageGeoReader();
                        var geom = gpkgReader.Read(blob);
                        Assert.IsNotNull(geom);
                        Assert.IsInstanceOf<IPoint>(geom);
                        Assert.AreEqual(point, geom);
                        Assert.IsTrue(point.EqualsExact(geom));
                        Assert.AreEqual(coord, geom.Coordinate);
                        Assert.IsTrue(coord.Equals(geom.Coordinate));
                        Assert.IsTrue(coord.Equals3D(geom.Coordinate));
                        Assert.AreEqual(coord.Z, geom.Coordinate.Z);
                    }
                }
            });
        }

        /// <summary>
        /// spatialite isn't able to read Z value from written blob
        /// </summary>
        [Test]
        public virtual void New_pointZ_should_be_written()
        {
            var coord = new Coordinate(11.11, 22.22, 33.33);
            var point = GeometryFactory.Default.CreatePoint(coord);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_z];
INSERT INTO [sample_feature_table_z] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XYZ
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);

                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table_z]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());

                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);
                        var gpkgReader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XYZ
                        };
                        var geom = gpkgReader.Read(blob);
                        Assert.AreEqual(point, geom);
                        Assert.AreEqual(point.AsText(), geom.AsText());

                        Assert.IsTrue(reader.GetBoolean(1));
                        Assert.IsFalse(reader.GetBoolean(2));
                        string wkt = reader.GetString(3);
                        Assert.IsFalse(string.IsNullOrEmpty(wkt));
                        string astext = point.AsText().Replace("POINT (", "POINT(");
                        Assert.AreEqual(astext, wkt);
                    }
                }
            });
        }

        [Test]
        public virtual void Existing_pointM_should_be_read()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 0.0 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.AreEqual(0.0, point.Z);
            Assert.AreEqual(44.44, point.M);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_m];
INSERT INTO [sample_feature_table_m] ([id], [geometry])
VALUES (1, gpkgMakePointM(@px, @py, @pm, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", point.X);
                    cmd.Parameters.AddWithValue("py", point.Y);
                    cmd.Parameters.AddWithValue("pm", point.M);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table_m]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);

                        var gpkgReader = new GeoPackageGeoReader(sequenceFactory,
                            new PrecisionModel(PrecisionModels.Floating), Ordinates.XYM);
                        var geom = gpkgReader.Read(blob);
                        Assert.IsNotNull(geom);
                        Assert.IsInstanceOf<IPoint>(geom);
                        Assert.AreEqual(point, geom);
                        Assert.IsTrue(point.EqualsExact(geom));
                        Assert.AreEqual(point.Coordinate, geom.Coordinate);
                        Assert.IsTrue(point.Coordinate.Equals(geom.Coordinate));
                        Assert.AreEqual(point.M, ((Point)geom).M);
                    }
                }
            });
        }

        /// <summary>
        /// spatialite isn't able to read M value from written blob
        /// </summary>
        [Test]
        public virtual void New_pointM_should_be_written()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 0.0 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.AreEqual(0.0, point.Z);
            Assert.AreEqual(44.44, point.M);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_m];
INSERT INTO [sample_feature_table_m] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XYM
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table_m]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());

                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);
                        var gpkgReader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XYM
                        };
                        var geom = gpkgReader.Read(blob);
                        Assert.AreEqual(point, geom);
                        Assert.AreEqual(point.AsText(), geom.AsText());

                        Assert.IsFalse(reader.GetBoolean(1));
                        Assert.IsTrue(reader.GetBoolean(2));
                        string wkt = reader.GetString(3);
                        Assert.IsFalse(string.IsNullOrEmpty(wkt));
                        string astext = point.AsText().Replace("POINT (", "POINT(");
                        Assert.AreEqual(astext, wkt);
                    }
                }
            });
        }

        [Test]
        public virtual void Existing_pointZM_should_be_read()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYZM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 33.33 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.AreEqual(33.33, point.Z);
            Assert.AreEqual(44.44, point.M);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_zm];
INSERT INTO [sample_feature_table_zm] ([id], [geometry])
VALUES (1, gpkgMakePointZM(@px, @py, @pz, @pm, 4326));";
                    cmd.CommandText = sql;
                    cmd.Parameters.AddWithValue("px", point.X);
                    cmd.Parameters.AddWithValue("py", point.Y);
                    cmd.Parameters.AddWithValue("pz", point.Z);
                    cmd.Parameters.AddWithValue("pm", point.M);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT geometry
FROM [sample_feature_table_zm]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());
                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);

                        var gpkgReader = new GeoPackageGeoReader(sequenceFactory,
                            new PrecisionModel(PrecisionModels.Floating), Ordinates.XYZM);
                        var geom = gpkgReader.Read(blob);
                        Assert.IsNotNull(geom);
                        Assert.IsInstanceOf<IPoint>(geom);
                        Assert.AreEqual(point, geom);
                        Assert.IsTrue(point.EqualsExact(geom));
                        Assert.AreEqual(point.Coordinate, geom.Coordinate);
                        Assert.IsTrue(point.Coordinate.Equals(geom.Coordinate));
                        Assert.IsTrue(point.Coordinate.Equals3D(geom.Coordinate));
                        Assert.AreEqual(point.Z, ((Point)geom).Z);
                        Assert.AreEqual(point.M, ((Point)geom).M);
                    }
                }
            });
        }

        /// <summary>
        /// spatialite isn't able to read Z+M values from written blob
        /// </summary>
        [Test]
        public virtual void New_pointZM_should_be_written()
        {
            var sequenceFactory = new DotSpatialAffineCoordinateSequenceFactory(Ordinates.XYZM);
            var coordinateSequence = sequenceFactory.Create(
                new[] { 11.11, 22.22 },
                new[] { 33.33 },
                new[] { 44.44 });
            var factory = new GeometryFactory(sequenceFactory);
            var point = factory.CreatePoint(coordinateSequence);
            Assert.AreEqual(33.33, point.Z);
            Assert.AreEqual(44.44, point.M);

            DoTest(conn =>
            {
                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
DELETE FROM [sample_feature_table_zm];
INSERT INTO [sample_feature_table_zm] ([id], [geometry])
VALUES (1, @pbytes);";
                    cmd.CommandText = sql;
                    var writer = new GeoPackageGeoWriter
                    {
                        HandleOrdinates = Ordinates.XYZM
                    };
                    byte[] bytes = writer.Write(point);
                    cmd.Parameters.AddWithValue("pbytes", bytes);
                    int ret = cmd.ExecuteNonQuery();
                    Assert.AreEqual(1, ret);
                }

                using (var cmd = conn.CreateCommand())
                {
                    const string sql = @"
SELECT
    geometry
   ,ST_Is3D(geometry)
   ,ST_IsMeasured(geometry)
   ,ST_AsText(GeomFromGPB(geometry))   
FROM [sample_feature_table_zm]
WHERE [id] = 1;";
                    cmd.CommandText = sql;
                    using (var reader = cmd.ExecuteReader())
                    {
                        Assert.IsTrue(reader.Read());

                        byte[] buffer = new byte[10000];
                        long l = reader.GetBytes(0, 0, buffer, 0, buffer.Length);
                        Assert.IsTrue(l > 0);
                        byte[] blob = new byte[l];
                        Array.Copy(buffer, blob, l);
                        var gpkgReader = new GeoPackageGeoReader
                        {
                            HandleOrdinates = Ordinates.XYZM
                        };
                        var geom = gpkgReader.Read(blob);
                        Assert.AreEqual(point, geom);
                        Assert.AreEqual(point.AsText(), geom.AsText());

                        Assert.IsTrue(reader.GetBoolean(1));
                        Assert.IsTrue(reader.GetBoolean(2));
                        string wkt = reader.GetString(3);
                        Assert.IsFalse(string.IsNullOrEmpty(wkt));
                        string astext = point.AsText().Replace("POINT (", "POINT(");
                        Assert.AreEqual(astext, wkt);
                    }
                }
            });
        }
    }
}
