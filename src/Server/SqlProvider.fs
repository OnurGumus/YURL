module internal SqlProvider

open FSharp.Data.Sql
open FSharp.Data.Sql.Common


[<Literal>]
let resolutionPath = __SOURCE_DIRECTORY__ + @"/libs"

[<Literal>]
let schemaLocation = __SOURCE_DIRECTORY__ + @"/../Web/Database/Schema.sqlite"
#if DEBUG

[<Literal>]
let connectionString =
      @"Data Source=" + __SOURCE_DIRECTORY__ + @"/Shorten.db;"

#else

[<Literal>]
let connectionString = @"Data Source=Database/Shorten.db;"

#endif


type Sql =
    SqlDataProvider<
        DatabaseProviderTypes.SQLITE,
        SQLiteLibrary=SQLiteLibrary.MicrosoftDataSqlite,
        ConnectionString=connectionString,
        ResolutionPath= resolutionPath,
     //   ContextSchemaPath=schemaLocation,
        CaseSensitivityChange=CaseSensitivityChange.ORIGINAL
     >
