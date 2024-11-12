using System;  
using System.IO; 
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

/*********************************************************************/
//***** DeltaSQL Version (semantic version) **************************/
/*********************************************************************/
const string DeltaSQL_version = "1.01.003";
/*********************************************************************/
/*********************************************************************/


/*********************************************************************/
/*********************************************************************/
/****** WHAT THIS SCRIPT DOES ****************************************/
/*********************************************************************/
/*********************************************************************/

/*

What this script does: 

0.  Check and update compile DeltaSQL Version
1.  Set script constants 
2.  Create a config table with following annotations (this table is ignored in git):
        - Disabled SQL scripts
        - Path to SQL scripts
3.  Set necessary tabular shared expressions:
        - DataWarehouseDatabaseSource (must be filled)
        - DataWarehouseServerSource (must be filled)
        - IncRefreshFormat
        - NativeQuery
        - RangeEnd
        - RangeStart
4.  Load SQL script files
5.  Check that SQL script files are named correctly 
6.  Clear disabled SQL scripts annotation in config table
7.  Generate m-expressions for each table (SQL script)
8.  Add each generated m-expression as table & partition to model
        - Scripts that contain the tag 'DeltaSQL-disable' are ignored and added to the Disabled SQL scripts annotation in the config table
        - Table prefixes are substituted with prefixes that are friendly for the tabular model
        - For incremental tables the SourceExpression property is updated/added
        - For normal tables the partition expression is updated/added
        - if a table/partition has already been created, only the expression is updated (the table is not re-created)
9.  Set discourage implicit measures to true
10. Add TST and RTL template tables
11. Hide tables that always need to be hidden and adds them to their table groups

*/



/*********************************************************************/
/****** SET SCRIPT CONSTANTS *****************************************/
/*********************************************************************/
DeltaSQL_Version.Instance.SetVersion(DeltaSQL_version);

var delta_sql_tag = "-- Added by DeltaSQL --";
var required_data_source_expression = "NativeQuery";
var required_refresh_format_expression = "IncRefreshFormat";
var config_table_name = "__DeltaSQL_config";
var annotation_sql_path_name = "DeltaSQL_00_path";
var annotation_disabled_tables_name = "DeltaSQL_50_disabled_scripts";
var annotation_tabletype = "DeltaSQL_60_TableType";

// Prefixes coming from files
var calendar_table_prefix = "CAL - ";
var fact_table_prefix = "FCT - ";
var bridge_table_prefix = "BRI - ";
var dimension_table_prefix = "DIM - ";
var test_table_prefix = "TST - ";
var rtl_table_prefix = "RTL - ";

// New table prefixes (that are used in the cube)
var new_calendar_table_prefix = "";
var new_fact_table_prefix = "$ ";
var new_bridge_table_prefix = "~ BRI - ";
var new_dimension_table_prefix = "~ DIM - ";
var new_test_table_prefix = "~ TST - ";
var new_rtl_table_prefix = "~ RTL - ";

// Define M strings
var range_start_parameter_expression =
@"#datetime(1902, 1, 1, 0, 0, 0) meta [IsParameterQuery=true, Type=""DateTime"", IsParameterQueryRequired=true]";

var range_end_parameter_expression =
@"#datetime(2090, 1, 1, 0, 0, 0) meta [IsParameterQuery=true, Type=""DateTime"", IsParameterQueryRequired=true]";

var inc_refresh_format_function_expression =
@"(dt as datetime) =>
let
    DateTimeConverted = DateTime.ToText(dt, ""yyyy-MM-dd""),
    Source = ""CAST('"" & DateTimeConverted & ""' AS DATE)""
in
    Source
";

var native_query_expression =
@"(query as text) =>
let
    Source = Sql.Database(DataWarehouseServerSource, DataWarehouseDatabaseSource),
    QueryResults = Value.NativeQuery(Source, query)
in
    QueryResults


";

/*********************************************************************/
/****** ADD CONFIG TABLE *********************************************/
/*********************************************************************/

//Only update these Config Table Settings on creation
if (!Model.Tables.Contains(config_table_name)) { 

    var table = Model.AddCalculatedTable(config_table_name, GenerateEmptyTableDAX());
    table.SetAnnotation(annotation_sql_path_name, "");
    table.SetAnnotation(annotation_disabled_tables_name, "");

}
//Always update these Config Table Settings
if (Model.Tables.Contains(config_table_name)) { 
	
	var table = Model.Tables[config_table_name];
	table.IsHidden = true;
	table.TableGroup = "⚙️ Config";
	table.SetAnnotation(annotation_tabletype, "DeltaConfig");

}

/*********************************************************************/
/****** ADD SHARED EXPRESSIONS ***************************************/
/*********************************************************************/

// Add DataWarehouseServer expression
if (!Model.Expressions.Contains("DataWarehouseServerSource") ) { 

    var expression = Model.AddExpression("DataWarehouseServerSource");
    expression.Kind = ExpressionKind.M;
    expression.Expression = "\"input server name here\" meta [IsParameterQuery=true, Type=\"Text\", IsParameterQueryRequired=true]";

}

// Add DataWarehouseDatabase expression
if (!Model.Expressions.Contains("DataWarehouseDatabaseSource") ) { 

    var expression = Model.AddExpression("DataWarehouseDatabaseSource");
    expression.Kind = ExpressionKind.M;
    expression.Expression = "\"input database name here\" meta [IsParameterQuery=true, Type=\"Text\", IsParameterQueryRequired=true]";

}


// Add NativeQuery expression
if (!Model.Expressions.Contains(required_data_source_expression) ) { 

    var expression = Model.AddExpression("NativeQuery");
    expression.Kind = ExpressionKind.M;    
    expression.Expression = native_query_expression;

}

// Add IncRefresh expression
if (!Model.Expressions.Contains(required_refresh_format_expression) ) { 

    var expression = Model.AddExpression("IncRefreshFormat");
    expression.Kind = ExpressionKind.M;    
    expression.Expression = inc_refresh_format_function_expression;

}

// Add incremental refresh parameters
if (!Model.Expressions.Contains("RangeStart")) {
    
    var expression = Model.AddExpression("RangeStart");
    expression.Kind = ExpressionKind.M;    
    expression.Expression = range_start_parameter_expression;
}

if (!Model.Expressions.Contains("RangeEnd")) {
    
    var expression = Model.AddExpression("RangeEnd");
    expression.Kind = ExpressionKind.M;    
    expression.Expression = range_end_parameter_expression;
    
}

/*********************************************************************/
/****** LOAD SQL SCRIPT FILES ****************************************/
/*********************************************************************/

//Get path to SQL files
var path = Model.Tables[config_table_name].GetAnnotation(annotation_sql_path_name) + "\\";

//Stop execution if path is empty
if (path == "\\") { 

    Info("The annotation " + annotation_sql_path_name + " in the " + config_table_name + " table must contain a fully qualified filepath to the SQL files. The path should not end with at '\\'. Fill it out in the table that has been created.");

    return; 
}; 

var d = new DirectoryInfo(path);    
var files = d.GetFiles();
var files_sql = files.Where(t => t.Name.EndsWith(".sql"));
var m_expression_list = new Dictionary<string, string>();
var m_expression =
@"let 

source =

NativeQuery(

""

##SQL_CODE##

""

)

in

source";

/*********************************************************************/
/****** CHECK SQL FILE NAMING CONVENTIONS ****************************/
/*********************************************************************/

foreach (var file in files_sql) {
    
    var file_name = file.Name;
    
    if (!file_name.StartsWith(dimension_table_prefix) && !file_name.StartsWith(fact_table_prefix) && !file_name.StartsWith(bridge_table_prefix) && !file_name.StartsWith(calendar_table_prefix)) { 
        throw new Exception("File :" + file_name + " does not start with either 'FCT - ' or 'DIM - ' or 'BRI - ' or 'CAL - '");  
    }

}

/*********************************************************************/
/****** CLEAR DISABLES SQL SCRIPTS ANNOTATION ************************/
/*********************************************************************/

Model.Tables[config_table_name].SetAnnotation(annotation_disabled_tables_name, "");

/*********************************************************************/
/****** GENERATE M-EXPRESSIONS FOR EACH SQL SCRIPT *******************/
/*********************************************************************/

foreach (var file in files_sql) {
    
    // Define working variables
    var delta_sql_where_clause_tag_search_string = @"\/\*DeltaSQL-iwc:##column_name##(.*)\*\/";
    var incremental_columns_search_string = @"(?<=\/\*DeltaSQL-iwc:)(.*)(?=\*\/)";
    var delta_sql_where_clause = @"WHERE ##column_name## >= "" & IncRefreshFormat(RangeStart) & "" AND ##column_name## < "" & IncRefreshFormat(RangeEnd) & """;
    var delta_sql_disable_tag_search_string = @"\/\*DeltaSQL-disable\*\/";    

    var incremental_columns_search_regex = new Regex(incremental_columns_search_string);
    var delta_sql_disable_tag_search_string_regex = new Regex(delta_sql_disable_tag_search_string);
    var file_name = file.Name;
    var file_path = path + file_name;
    var file_contents = File.ReadAllText(file_path);
    var incremental_column_matches = incremental_columns_search_regex.Matches(file_contents);
    var table_disable_matches = delta_sql_disable_tag_search_string_regex.Matches(file_contents);
    var table_name = file_name.Replace(".sql", "");

    if (table_disable_matches.Any()) { 
        
        var current_annotation_value = Model.Tables[config_table_name].GetAnnotation(annotation_disabled_tables_name);
        Model.Tables[config_table_name].SetAnnotation(annotation_disabled_tables_name, current_annotation_value + file_name + " \n" );
        continue; 

    }

    if (file_name.Contains(" - (inc) - ")) { // Incremental Table
        
        if (!incremental_column_matches.Any()) {
            throw new Exception("Table: " + table_name + " is configured as incremental but does not have any /*DeltaSQL-iwc:[column_name]*/ tags");
        }
        
        // Add SQL code
        var expression = m_expression.Replace("##SQL_CODE##", file_contents);

        // Add incremental where clauses
        foreach (Match column_match in incremental_column_matches) {
            
            var column_name = column_match.Value;
            var where_clause = delta_sql_where_clause.Replace("##column_name##", column_name);
            var specific_where_clause_search_string = delta_sql_where_clause_tag_search_string.Replace("##column_name##", column_name);
            expression = Regex.Replace(expression, specific_where_clause_search_string, where_clause);
        
        }
        
        //Add to list of expressions
        m_expression_list.Add(table_name, expression);
        
    } else { // Full load table
        
        var expression = m_expression.Replace("##SQL_CODE##", file_contents);
        m_expression_list.Add(table_name, expression);

    }
}  

/*********************************************************************/
/****** ADD TABLES TO MODEL ******************************************/
/*********************************************************************/

foreach (var kvp in m_expression_list) {
    
    var table_name = kvp.Key;
    var new_table_name = "";
    var m_expression_code = kvp.Value;
	
    if (table_name.StartsWith(calendar_table_prefix))   { new_table_name = table_name.Replace(calendar_table_prefix,    new_calendar_table_prefix); }
    if (table_name.StartsWith(fact_table_prefix))       { new_table_name = table_name.Replace(fact_table_prefix,        new_fact_table_prefix); }
    if (table_name.StartsWith(bridge_table_prefix))     { new_table_name = table_name.Replace(bridge_table_prefix,      new_bridge_table_prefix); }
    if (table_name.StartsWith(dimension_table_prefix))  { new_table_name = table_name.Replace(dimension_table_prefix,   new_dimension_table_prefix); }
    if (table_name.StartsWith(test_table_prefix))       { new_table_name = table_name.Replace(test_table_prefix,        new_test_table_prefix); }
    if (table_name.StartsWith(rtl_table_prefix))        { new_table_name = table_name.Replace(rtl_table_prefix,         new_rtl_table_prefix); }

    if (table_name.Contains("(inc) -"))               { new_table_name = new_table_name.Replace("(inc) - ",              ""); } //Remove inc part from name

	/****** Handle calendar table and skip rest of loop in that case *****/

	// In case calendar table does not exist
	if (table_name.StartsWith(calendar_table_prefix) && !Model.Tables.Contains(new_table_name)) {
				
		var table = Model.AddTable(new_table_name, false);
		table.Description = delta_sql_tag;
		table.AddMPartition(new_table_name, m_expression_code);
		Model.Tables[new_table_name].TableGroup = "🗃 Dimension Tables";
		Model.Tables[new_table_name].SetAnnotation("DeltaSQL_IsDateTable", "true");
		Model.Tables[new_table_name].SetAnnotation(annotation_tabletype, "Dimension_SQL");
		continue;

	}

	// In case calendar table does exist
	if (table_name.StartsWith(calendar_table_prefix) && Model.Tables.Contains(new_table_name)) {

		Model.Tables[new_table_name].Partitions[new_table_name].Expression = m_expression_code;
		Model.Tables[new_table_name].TableGroup = "🗃 Dimension Tables";
		Model.Tables[new_table_name].SetAnnotation(annotation_tabletype, "Dimension_SQL");
		continue;

	}

    /****** Continue with other tables *****/

    if (!Model.Tables.Contains(new_table_name) ) { // If table do not exist
        

        
        if (table_name.Contains("- (inc) -")) { // Incremental
            
            var table = Model.AddTable(new_table_name, false);
            table.Description = delta_sql_tag;
            table.AddMPartition(new_table_name, m_expression_code);
            table.EnableRefreshPolicy = true;
            table.IncrementalGranularity = RefreshGranularityType.Month;
            table.IncrementalPeriods = 6;
            table.RollingWindowGranularity = RefreshGranularityType.Year;
            table.RollingWindowPeriods = 30;
            table.SourceExpression = m_expression_code;
			table.IsHidden = true; 
            
        } else { // Normal
			
			var table = Model.AddTable(new_table_name, false);
			table.Description = delta_sql_tag;
			table.AddMPartition(new_table_name, m_expression_code);
			table.IsHidden = true; 
            
        }

    } else { // If table do exist

        if (table_name.Contains("- (inc) -")) { // Incremental
            
            Model.Tables[new_table_name].SourceExpression = m_expression_code; // We only change the source expression, allowing for editing of incremental refresh configuration
            
            if (Model.Tables[new_table_name].Partitions.Contains(new_table_name)) {
                    
                Model.Tables[new_table_name].Partitions[new_table_name].Expression = m_expression_code; // Update in case incremental partition hasn't yet been created
            
            }
            
        } else { // Normal
			
            Model.Tables[new_table_name].Partitions[new_table_name].Expression = m_expression_code;
			  
        }
       
    }
    
}

/*********************************************************************/
/****** SET DISCOURAGE IMPLICIT MEASURES TO TRUE *********************/
/*********************************************************************/

Model.DiscourageImplicitMeasures = true;

/*********************************************************************/
/****** ADD TST AND RTLS TEMPLATE TABLES *****************************/
/*********************************************************************/

if (!Model.Tables.Contains(new_test_table_prefix + "_Template")) { Model.AddCalculatedTable(new_test_table_prefix + "_Template", "{{\"\"}}"); }
if (!Model.Tables.Contains(new_rtl_table_prefix + "_Template")) { Model.AddCalculatedTable(new_rtl_table_prefix + "_Template", "{{\"\"}}"); }

/*********************************************************************/
/****** HIDE AND ADD TO TABLE GROUPS *********************************/
/*********************************************************************/

var raw_tables = Model.Tables.Where( 
                    t => 
                    t.Name.StartsWith(new_bridge_table_prefix) || 
                    t.Name.StartsWith(new_fact_table_prefix) || 
                    t.Name.StartsWith(new_dimension_table_prefix) ||
                    t.Name.StartsWith(new_test_table_prefix) ||
                    t.Name.StartsWith(new_rtl_table_prefix)
                );

foreach (var tbl in raw_tables) {

    if (tbl.Name.StartsWith(new_bridge_table_prefix)) { tbl.TableGroup = "🈂️ Bridge Tables"; tbl.SetAnnotation(annotation_tabletype, "Bridge_SQL"); tbl.IsHidden = true; tbl.IsPrivate = true; }
    if (tbl.Name.StartsWith(new_fact_table_prefix)) { tbl.TableGroup = "🔢 Fact Tables"; tbl.SetAnnotation(annotation_tabletype, "Fact_SQL"); }
    if (tbl.Name.StartsWith(new_dimension_table_prefix)) { tbl.TableGroup = "🔽 Dimension Tables (raw)"; tbl.SetAnnotation(annotation_tabletype, "Dimension_SQL"); tbl.IsHidden = true; tbl.IsPrivate = true; }
    if (tbl.Name.StartsWith(new_test_table_prefix)) { tbl.TableGroup = "🚫 Test Tables"; tbl.SetAnnotation(annotation_tabletype, "TestTable_DAX"); tbl.IsHidden = true; tbl.IsPrivate = true; }
    if (tbl.Name.StartsWith(new_rtl_table_prefix)) { tbl.TableGroup = "🚩 Reverse ETL Tables"; tbl.SetAnnotation(annotation_tabletype, "ReverseETL_DAX"); tbl.IsHidden = true; tbl.IsPrivate = true; }

}

/************************************************************************************/
/*************** Object & Function Section START ************************************/
/************************************************************************************/


//Create DAX for empty table
public string GenerateEmptyTableDAX() {

return
@"DATATABLE(
    ""z_dummy"", STRING,
    {{}}
)
";

}

/*** Versioning 																  ***/
/*** MAJOR version when you make incompatible API changes                         ***/
/*** MINOR version when you add functionality in a backward compatible manner     ***/
/*** PATCH version when you make backward compatible bug fixes                    ***/
public class DeltaSQL_Version 
{

	protected string _VersionID;

	private int _MayorVersion;
	private int _MinorVersion;
	private int _PatchVersion;
	
	private string DeltaTool = "DeltaSQL";
	private string _annotation = "DeltaSQL_Version";

	//Singleton pattern
    private DeltaSQL_Version() {}
    private static DeltaSQL_Version instance = null;
    public static DeltaSQL_Version Instance {
        get {
            if (instance == null) {
                instance = new DeltaSQL_Version();
            }
            return instance;
        }
    }

	public void SetVersion(string VersionID) {

		if(!string.IsNullOrEmpty(_VersionID))
			return;	//Do not update multiple times

		//VersionNumber have to follow the standard X.XX.XXX
		var VersionArray = VersionID.Split(".");

		if(VersionArray.Count() != 3)
			throw new Exception(DeltaTool + ": version syntax does not follow X.XX.XXX structure as specified");

		//Set Version
		_VersionID = VersionID;

		//Framework Version
		_MayorVersion = Convert.ToInt16(VersionArray[0]);
		_MinorVersion = Convert.ToInt16(VersionArray[1]);
		_PatchVersion = Convert.ToInt16(VersionArray[2]);

		//Get Version Annotation
		var _currentVersion = Model.GetAnnotation(_annotation);
		if(string.IsNullOrEmpty(_currentVersion))
			_currentVersion = _VersionID;

		var _currentVersionArray = _currentVersion.Split(".");

		if (_currentVersionArray.Count() != 3)
			throw new Exception(DeltaTool + ": Version syntax does not follow X.XX.XXX structure as specified");

		//Current Version
		var _currentMayorVersion = Convert.ToInt16(_currentVersionArray[0]);
		var _currentMinorVersion = Convert.ToInt16(_currentVersionArray[1]);
		var _currentPatchVersion = Convert.ToInt16(_currentVersionArray[2]);
	
		//Check that the mayor version is changed
		if(_currentMayorVersion > _MayorVersion)
			throw new Exception(DeltaTool + ": This model was compiled with a newer mayor version (" + _currentVersion + ") of DeltaSQL. Please check that you are using the intended version (" + _VersionID + "). To change mayor version --> delete the model version annotation manually.");
		if(_currentMayorVersion < _MayorVersion)
			throw new Exception(DeltaTool + ": This model was compiled with an older mayor version (" + _currentVersion + ") of DeltaSQL. To upgrade to version (" + _VersionID + "), please delete the model version annotation manually.");
			
		//Check that minor version is changed
		if(_currentMinorVersion < _MinorVersion)
			Info(DeltaTool + " Note: This model was compiled with a prior minor version (" + _currentVersion + "). New features are available in version (" + _VersionID + ").");
		if(_currentMinorVersion > _MinorVersion)
			throw new Exception(DeltaTool + ": This model was compiled with a newer minor version (" + _currentVersion + "). Please check that you have the most current version (" + _VersionID + ").");
		

		//No check for patch version, we prefer silence ;-)
		if(_currentPatchVersion != _PatchVersion)
			Info(DeltaTool + " Note: This model was compiled with a different patch version (" + _currentVersion + "). This version is " + _VersionID + ".");

		//If we reach this point, we update the model version annotation
		Model.SetAnnotation(_annotation,_VersionID);
	}
}


/*****************************************************************/
/*************** Object & Function Section END *******************/
/*****************************************************************/