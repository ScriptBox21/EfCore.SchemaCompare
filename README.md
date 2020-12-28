# EfCore.SchemaCompare

If you are changing the schema of your database's schema outside of EF Core' migrations, say by using SQL change scripts, then this library can quickly tell you if the a specific database schema and EF Core's `Model` of the database are in step.

The first number in the version number of this library defines what version of EF Core it works for. e.g. EfCore.SchemaCompare version 5 is works with to EF Core 5.

The EfCore.SchemaCompare library (shortened to EfSchemaCompare in the documentations) is available on [NuGet as EfCore.SchemaCompare](#) and is an open-source library under the MIT licence. See [ReleaseNotes](https://github.com/JonPSmith/EfCore.SchemaCompare/blob/master/ReleaseNotes.md) for details of changes and information on versions before EF Core 5.

**TABLE OF CONTENT**

1. [Introduction to how EfCore.SchemaCompare]

*NOTE: I use the term *entity class* for classes mapped to the database by EF Core.*
## List of checks made

### Stage 1 - checks on EF Core side

- **Table/View exists**: That a table or view that an entity class is mapped exists. This checks both table/view name and schema name
- **Property/Column:**  exists, database type (including size and precision), nullability, computed column (including persistence), column default value, when updated (e.g. column is updated `OnAdd` for a int primary key which is provided by the database)
- **Primary key:** SQL constraint name, properties
- **Foreign keys:** SQL constraint name, Delete behavior, properties
- **Indexes:** SQL constraint name, Unique/not unique, properties

### Stage 2 - checks on database

- Unused tables or views
- Unused columns
- Unused Indexes

## List of limitations

- Cannot detect [Owned Type with `Required` option](https://docs.microsoft.com/en-us/ef/core/what-is-new/ef-core-5.0/whatsnew#required-11-dependents) (i.e. not null) - This is a limitation of EF Core (see EF Core issue [#23758](https://github.com/dotnet/efcore/issues/23758)).
- Cannot correctly check Table-per-Type classes. This is a limitation of EF Core.
- Cannot compare database entries using ignore case. That is a EF Core 5 limitation.

The following are things I haven't bothered to check.

- Checking of Alternative keys
- Checking of collations
- Checking of sequences

## Introduction to how EfSchemaCompare works

EfSchemaCompare uses two EF Core features to get EF Core's internal schema and the database's schema. They are

1. EF Core's `Model` property in your application's DbContext. This provides the internal version of the database schema that EF Core builds from looking at the entity classes and any EF Core configuration attributes/methods.
2. EF Core's [Reverse Engineering](https://docs.microsoft.com/en-us/ef/core/managing-schemas/scaffolding) service, which allows me to access an actual database schema.

The fun part is comparing these two sources, especially with all the different types of configurations that EF Core can handle. The diagram shown below shows using EfSchemaCompare to check a test database that you updated with some SQL migration scripts against the current EF Core's `Model`.

![EfSchemaCompare diagram](https://github.com/JonPSmith/EfCore.SchemaCompare)

## How to use EfSchemaCompare

Here is a example of using the EfSchemaCompare feature

```c#
[Fact]
public void CompareViaContext()
{
    //SETUP
    using (var context = new BookContext(_options))
    {
        var comparer = new CompareEfSql();

        //ATTEMPT
        //This will compare EF Core model of the database with the database that the context's connection points to
        var hasErrors = comparer.CompareEfWithDb(context); 

        //VERIFY
        //The CompareEfWithDb method returns true if there were errors. 
        //The comparer.GetAllErrors property returns a string, with each error on a separate line
        hasErrors.ShouldBeFalse(comparer.GetAllErrors);
    }
}
```

### Different parameters to the `CompareEfWithDb` method

1. The `CompareEfWithDb` method can take multiple DbContexts, known as *bounded contexts* (see chapter 13, section 13.4.8 in the second edition of my book [Entity Framework Core in Action(https://bit.ly/EfCoreBookEd2)]). You can add as many contexts and they are compared to one database.
2. You can also provide a string that points to the database as the first parameter. It can have two forms:
   - It will use the string as a connection string name in the test's `appsetting.json` file.
   - If no connection string is found in the `appsetting.json` file it assumes it is a connection string.

See below for an example of both of of these options:
```c#
[Fact]
public void CompareBookThenOrderAgainstBookOrderDatabaseViaAppSettings()
{
    //SETUP
    //... I have left out how the options are created
    //This is the name of a connection string in the appsetting.json file in your test project
    const string connectionStringName = "BookOrderConnection";
    using (var context1 = new BookContext(options1))
    using (var context2 = new OrderContext(options2))
    {
        var comparer = new CompareEfSql();

        //ATTEMPT
        //Its starts with the connection string/name  and then you can have as many contexts as you like
        var hasErrors = comparer.CompareEfWithDb(connectionStringName, context1, context2);

        //VERIFY
        hasErrors.ShouldBeFalse(comparer.GetAllErrors);
    }
}
```

### Using with database provider not installed in `EfCore.SchemaCompare` library

The `EfCore.SchemaCompare` library only contains the SqlServer and Sqlite database providers. But if you want to run the compare with a specific database provider you can do that using the version with takes in a `IDesignTimeServices` for your database provider which contains the reverse engineering feature. For instance if you wanted to check a PostgreSql database you would use the following code shown below.

```c#
[Fact]
public void TestCompareEfSqlPostgreSql()
{
    //SETUP
    using (var context = new BookContext(_options))
    {
        var comparer = new CompareEfSql();

        //ATTEMPT
        //This will use the database provider design time type you gave to get the database information
        var hasErrors = comparer.CompareEfWithDb<NpgsqlDesignTimeServices>(context));

        //VERIFY
        hasErrors.ShouldBeFalse(comparer.GetAllErrors);
    }
}
```

## What errors look like

The `comparer.GetAllErrors` property will return a string with each error separated by the `Environment.NewLine` string. Below is an example of an error

```text
"DIFFERENT: MyEntity->Property 'MyString'. Expected = varchar(max), found = nvarchar(max)"
```

The error above says

- `DIFFERENT:` There is a difference between EF Core and the database  (other settings are `NotInDatabase`, `ExtraInDatabase`)
- `MyEntity->Property 'MyString', column type` gives a description of what was checked
- `Expected = varchar(max)` says what EF Core thought it should be
- `found = nvarchar(max)` says what the database setting was

Here is another error coming from stage 2 where it checks the database side.

```text
EXTRA IN DATABASE: Table 'MyEntites', column name. Found = MyEntityId
```

This says that there is a column called `MyEntityId` in the table `MyEntites` that hasn't got a property in the entity class mapped to the `MyEntites` table.

*NOTE: When errors contain the word `Table` it can be a SQL Table or View.*

## How to suppress errors

In a few cases you will get errors that aren't correct (see limitations) or not relevant. In these cases you might want to suppress those errors. There are two way to do this, with the first being the easiest. Both use the `CompareEfSqlConfig` class.

### Suppress errors via `IgnoreTheseErrors`

In this approach you capture the error strings you want to ignore and return them as a string, with each error separated by the newline, `'\n'`, character. You feed the errors via the configuration's `IgnoreTheseErrors` method. See an example below

```c#
        public void CompareTptContextSuppressViaIgnoreTheseErrors()
        {
            //SETUP
            var options = this.CreateUniqueClassOptions<TptDbContext>();
            using var context = new TptDbContext(options);
            context.Database.EnsureClean();

            var config = new CompareEfSqlConfig();
            //This converts the error strings back CompareLog classes (see next example) which suppresses these errors 
            config.IgnoreTheseErrors(@"DIFFERENT: TptVer1->PrimaryKey 'PK_TptBases', constraint name. Expected = PK_TptBases, found = PK_TptVer1
DIFFERENT: TptVer1->Property 'Id', value generated. Expected = OnAdd, found = Never
DIFFERENT: TptVer1->Property 'MyVer1Int', nullability. Expected = NULL, found = NOT NULL
DIFFERENT: TptVer1->ForeignKey 'FK_TptVer1_TptBases_Id', delete behavior. Expected = ClientCascade, found = NoAction
DIFFERENT: Entity 'TptVer1', constraint name. Expected = PK_TptBases, found = PK_TptVer1
DIFFERENT: TptVer2->PrimaryKey 'PK_TptBases', constraint name. Expected = PK_TptBases, found = PK_TptVer2
DIFFERENT: TptVer2->Property 'Id', value generated. Expected = OnAdd, found = Never
DIFFERENT: TptVer2->Property 'MyVer2Int', nullability. Expected = NULL, found = NOT NULL
DIFFERENT: TptVer2->ForeignKey 'FK_TptVer2_TptBases_Id', delete behavior. Expected = ClientCascade, found = NoAction
DIFFERENT: Entity 'TptVer2', constraint name. Expected = PK_TptBases, found = PK_TptVer2");

            var comparer = new CompareEfSql(config);

            //ATTEMPT
            var hasErrors = comparer.CompareEfWithDb(context);

            //VERIFY
            hasErrors.ShouldBeFalse(comparer.GetAllErrors);
        }
    }
```

### Suppress errors via `AddIgnoreCompareLog`

The other approach is useful when you want to suppress a general set of errors, but it is a bit complicated. Here is an example where it suppresses any errors found on the default value set on a column.

```c#
[Fact]
public void CompareSuppressViaViaAddIgnoreCompareLog()
{
    //SETUP
    var options = this.CreateUniqueClassOptions<BookContext>();
    using var context = new BookContext(options);
    context.Database.EnsureClean();

    var config = new CompareEfSqlConfig
    config.AddIgnoreCompareLog(new CompareLog(CompareType.Property, CompareState.Different, null, CompareAttributes.DefaultValueSql));
    var comparer = new CompareEfSql(config);

    //ATTEMPT
    var hasErrors = comparer.CompareEfWithDb(context);

    //VERIFY
    hasErrors.ShouldBeFalse(comparer.GetAllErrors);
}

```

## Other configuration options

You have already seen the class called `CompareEfSqlConfig` for suppressing errors. There is one other configuration property called `TablesToIgnoreCommaDelimited`, which allows you to control what table/views in the database are considered. By default (i.e. when `TablesToIgnoreCommaDelimited` is null) then `CompareEfSql` will only look at the tables/views in the database that your EF Core entity classes are mapped to. This provides an simple starting point. The other options are:

- Set `TablesToIgnoreCommaDelimited` to "" (i.e. empty string)  
This will check all the tables/Views in the database
- Set `TablesToIgnoreCommaDelimited` to a list of tables to ignore  
If there are tables in your database that EF Core doesn't access then you need to tell `CompareEfSql`
about them, otherwise it will output a message saying there are extra tables you are not accessing from EF Core.
You do this by providing a comma delimited list of table names, with an optional schema name if needed.
Here are two examples of a table name
  - `MyTable` - this has no schema, so the default schema of the database will be used
  - `dbo.MyTable` - this defines the schema to be `dbo`, - a full stop separates the schema name from the table name.

*NOTE: The comparison is case insensitive.*  

Here is an example of configuring the comparer to not look at the tables `Orders` and `LineItem`
```c#
var config = new CompareEfSqlConfig
{
    TablesToIgnoreCommaDelimited = "Orders,LineItem"
};
var comparer = new CompareEfSql(config);