# EntityFramework6.Ingres

An Entity Framework 6 provider for Ingres.

## Notes

- This provider only works with Entity Framework 6.
- This provider has not yet been published to nuget.org.

## Build

Before building EntityFramework6.Ingres please:

- Make sure Visual Studio 2017 is installed.
- On first time build, under Visual Studio --> Tools --> NuGet Package Manager -->
Manage NuGet Packages for Solution, select each of the four packages and restore

EntityFramework6.Ingres is built using a Rebuild in the EntityFramework6.Ingres Visual Studio project.


## Tests

To set up tests, create a test database and define the tests' connection string to the INGRES_TEST_DB environment variable. For example:

```
set INGRES_TEST_DB=Database=ingres_test_ef6;Port=II7;
```


## TODO

- Support is needed for modelBuilder.HasDefaultSchema() to override the default schema name of empty string.
- DbContext.Database.Exists(), DbContext.Database.Delete(), and DbContext.Database.Create() is a no-operation.
- LINQ support is limited. There is very limited support for .First(), .Top(), Skip(), Take(), etc.
- Database migrations, especially column migrations is limited.
