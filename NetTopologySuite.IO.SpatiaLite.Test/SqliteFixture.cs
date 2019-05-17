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
            string spatialiteRelName = is64bit
                ? "mod_spatialite-4.3.0a-win-amd64"
                : "mod_spatialite-4.3.0a-win-x86";
            string spatialiteRelPath = $"../../../../libs/{spatialiteRelName}";
            string spatialiteFullPath = Path.GetFullPath(spatialiteRelPath);
            Assert.IsTrue(Directory.Exists(spatialiteFullPath), $"path not found: {spatialiteFullPath}");

            string path = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Process);
            if (path.IndexOf(spatialiteRelName, StringComparison.OrdinalIgnoreCase) == -1)
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
                        Assert.AreEqual(coord, geom.Coordinate);
                        Assert.IsTrue(Double.IsNaN(geom.Coordinate.Z));
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
DELETE FROM [sample_feature_table];
INSERT INTO [sample_feature_table] ([id], [geometry])
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
                        Assert.AreEqual(coord, geom.Coordinate);
                        Assert.IsTrue(coord.Equals3D(geom.Coordinate));
                    }
                }
            });
        }
    }
}
