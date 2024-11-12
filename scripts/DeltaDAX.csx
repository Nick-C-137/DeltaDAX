using System;  
using System.IO; 
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

/*********************************************************************/
//***** DeltaDAX Version (semantic version) **************************/
/*********************************************************************/
const string DeltaDAX_version = "1.04.008";
/*********************************************************************/
/*********************************************************************/


/*****************************************************************/
/*****************************************************************/
/*************** WHAT THIS SCRIPT DOES ***************************/
/*****************************************************************/
/*****************************************************************/

/*
0.  Check and update compile DeltaDAX Version
1.  Set script constants such as prefixes
2.  Configure date tables (coming from DeltaSQL)
3.  Check DeltaDAX naming conventions for tables and columns
4.  Check data types for DeltaDAX columns
5.  Check that there doesn't exist more than 2 measure tables
6.  Set dax code skeleton for final dimension tables
7.  Loop through raw dimension tables
        - Produce skeleton parts of final dimension table
        - Produce final dax code expression for final dimension table
8.  Add the final dimension tables to the model
		- Set key column (_BK - )
		- Updating Descriptions from RAW tables based on SQL comment syntax >> DeltaSQL_Desc - MyDimension: MyDescription <<
9.	Create Last Updated Fact based on "last_updated" columns in all tables
10. Create relationships for fact tables
        - To dimension tables
        - To date tables
11. Create relationships for bridge tables
12. Create relationships for many to many columns
13. Create use relationship calculation group with calculation items
14. Add metrics tables
        - Metrics
        - Metrics (local)
15. Add base measures (based on '_MS - ' columns)
16. Add Description to all MEASURES
17. Move tables into table groups
        - Calculation group tables
        - 'TST - ' tables
        - 'RTL - ' tables
18. Move columns into display folders and add sort by for:
        - Date tables
        - Fact tables
        - Dimension tables

*/

/*****************************************************************/
/*************** SET SCRIPT CONSTANTS ****************************/
/*****************************************************************/
DeltaDAX_Version.Instance.SetVersion(DeltaDAX_version);

var bk_prefix = "_BK - ";
var dk_prefix = "_DK - ";
var ms_prefix = "_MS - ";
var mm_prefix = "_MM - ";
var dim_prefix = "~ DIM - ";
var fct_prefix = "$ ";
var fct_inc_prefix = "$ ";
var bri_prefix = "~ BRI - ";
var dim_final_prefix = "";
var ms_tbl_prefix = "📊 ";
var tst_prefix = "~ TST - ";
var rtl_prefix = "~ RTL - ";
var calc_group_table_group_name = "🧮 Calculation Groups";
var tst_table_group_name = "🚫 Test Tables";
var rtl_table_group_name = "🚩 Reverse ETL Tables";
var dimension_tables_table_group_name = "🗃 Dimension Tables";
var warnings = new List<string>();
var has_duped_bks_annotation = "DeltaSQL_GenUseRelationshipForBusinessKeys";
var has_duped_dks_annotation = "DeltaSQL_GenUseRelationshipForDateKeys";
var annotation_tabletype = "DeltaSQL_60_TableType";

/*****************************************************************/
/*************** CONFIGURE DATE TABLES ***************************/
/*****************************************************************/

var date_tables = Model.Tables.Where( t => t.GetAnnotation("DeltaSQL_IsDateTable") == "true");

foreach (var t in date_tables) {
	
	var table_name = t.Name;
	Model.Tables[table_name].DataCategory = "Time";
	Model.Tables[table_name].Columns["Date"].IsKey = true;

}


/*****************************************************************/
/*************** CHECK NAMING FOR TABLES & COLUMNS ***************/
/*****************************************************************/

foreach (var tbl in Model.Tables) {
    
    var tbl_name = tbl.Name;
    
    if (
           !tbl_name.StartsWith(fct_prefix)
        && !tbl_name.StartsWith(fct_inc_prefix)
        && !tbl_name.StartsWith(bri_prefix)
        && !tbl_name.StartsWith(dim_prefix)
        && !tbl_name.StartsWith(dim_final_prefix)
        && !tbl_name.StartsWith(ms_tbl_prefix)
        && !tbl_name.StartsWith(tst_prefix)
        && !tbl_name.StartsWith(rtl_prefix)
        && !tbl_name.Equals("__DeltaSQL_config")       

    ) {
		throw new Exception($"Table '{tbl_name}' violates the DeltaDAX prefix syntax rules for tables");
	}

    var characters_to_check_for_dupes_in_tables = new List<string>([
													 "~"
													,"$"
													,"-"
                                                    ,"#"
                                                    ,"@"
                                                    ,"§"
												]);

    foreach (var c in characters_to_check_for_dupes_in_tables) {
			
        var split_value_count = tbl_name.Split(c).Count();
    
        if (split_value_count > 2) {
            throw new Exception($"Table '{tbl_name}' contains the character '{c}' more than once which is not allowed.");
        }

    }


    if (tbl_name.StartsWith(tst_prefix) || tbl_name.StartsWith(rtl_prefix)) {

        var table_is_hidden = Model.Tables[tbl_name].IsHidden;

        if (!table_is_hidden) {throw new Exception($"Table '{tbl_name}' must be hidden"); }

    }

    if (tbl_name.StartsWith(fct_inc_prefix) || tbl_name.StartsWith(fct_prefix) || tbl_name.StartsWith(bri_prefix) || tbl_name.StartsWith(dim_prefix)) {

        var table_columns = Model.Tables[tbl_name].Columns;
        
        foreach (var col in table_columns) {
            
            var cole_name = col.Name;
            
            if (cole_name.StartsWith("_")) { // Only check columns part of DeltaDAX 
                
				if (
                       !cole_name.StartsWith(ms_prefix)
                    && !cole_name.StartsWith(dk_prefix)
                    && !cole_name.StartsWith(bk_prefix)
					&& !cole_name.StartsWith(mm_prefix)

                ) {
					throw new Exception($"Column '{cole_name}' in table '{tbl_name}' violates the DeltaDAX prefix syntax rules for columns");
				}
				
				var characters_to_check_for_dupes = new List<string>([
													 "-"
													,"*"
													,"/"
													,"|"
													,"*"
												]);
				
				foreach (var c in characters_to_check_for_dupes) {
			
					var split_value_count = cole_name.Split(c).Count();
				
					if (split_value_count > 2) {
						throw new Exception($"Column '{cole_name}' in table '{tbl_name}' contains the character '{c}' more than once which is not allowed.");
					}

				}

            }            
        }
    }

}

/*****************************************************************/
/*************** CHECK DeltaDAX COLUMN TYPES *********************/
/*****************************************************************/

var columns_to_type_check = Model.AllColumns;

foreach (var c in columns_to_type_check) {
	
	var col_name = c.Name;
	var tbl_name = c.Table.Name;
	
	if (col_name.StartsWith(bk_prefix) && c.DataType != DataType.String) {throw new Exception($"Column: '{tbl_name}'[{col_name}] does not have the type String"); }
	if (col_name.StartsWith(dk_prefix) && c.DataType != DataType.DateTime) {throw new Exception($"Column: '{tbl_name}'[{col_name}] does not have the type DateTime"); }
	if (col_name.StartsWith(ms_prefix) && (c.DataType != DataType.Double && c.DataType != DataType.Decimal && c.DataType != DataType.Int64)) {throw new Exception($"Column: '{tbl_name}'[{col_name}] does not have the type Double, Decimal or Integer"); }
		
}

/*****************************************************************/
/*************** CHECK FOR MAX 2 MEASURE TABLES ******************/
/*****************************************************************/

var metrics_tables = Model.Tables.Where( t => t.Name.StartsWith(ms_tbl_prefix));
if (metrics_tables.Count() > 2) { throw new Exception("The model can only contain 2 metrics table - use display folders to organize measures instead."); }




/*****************************************************************/
/*************** Set Column Descriptions from SQL expression *****/
/*****************************************************************/

var TableColumnsList = new List<TableColumns>();

var SourceTables = Model.Tables.Where( t => 
	t.GetAnnotation("DeltaSQL_60_TableType") != null								//Ensure that if type is not set, the function does not fail (Could be something custom)
	&& t.GetAnnotation("DeltaSQL_60_TableType").EndsWith("_SQL")					//Fact_SQL, Dimension_SQL, Bridge_SQL
	&& t.GetType() != typeof(TabularEditor.TOMWrapper.CalculationGroupTable)		//Calculation Groups are not relevant
);

foreach (var t in SourceTables) {
	//Output(t.Name + " | " + t.GetType() + " | " + t.ObjectTypeName);		//Table (Import)

	var SourceExpression = t.SourceExpression;	//All tables have SourceExpression, while it is null for non incrementatal tables

	try {
		//get the expression from table, while handling that it is stored differently for incremental tables
		var m_expression = string.IsNullOrEmpty(SourceExpression) ? t.Partitions[t.Name].Expression : SourceExpression;
		
		//Regex to find the description in the SQL expression
		var Col_Desc_Search_String = @"(?<=\/\*)(DeltaSQL_Desc - )(.*)(?=\*\/)";	// Example: /*DeltaSQL_Desc - myColumnName: myDescription*/
		//var Col_Desc_Search_String = @"(?<=\/\*DeltaSQL_Desc - )(.*)(?=\*\/)";
		var Col_Desc_Search_Regex = new Regex(Col_Desc_Search_String);
		var Col_Desc_Search_Matches = Col_Desc_Search_Regex.Matches(m_expression);


		foreach (Match match in Col_Desc_Search_Matches) {

			//Collect all identified Descriptions
			TableColumnsList.Add( new TableColumns()
			{
				 TableName 					= t.Name
				,ColumnDescriptionString 	= match.Value		//The class will parse the string
			});

		}
	} 
	catch (Exception e) {
		throw new Exception("Error while reading partition code for '" + t.Name + " | " + t.GetType() + "' to find description strings.");  
		return;
	}
}


foreach (var tc in TableColumnsList) {

	//Check if column exists
	if (!Model.Tables[tc.TableName].Columns.Contains(tc.ColumnName))
		throw new Exception("DeltaSQL_Desc :: Error setting description of column. '" + tc.TableName + "' >> '" + tc.ColumnName + "' | " + tc.Description); 
		
	//Set column description based on DeltaFact syntax :: Example: /*DeltaSQL_Desc - MyColumnName: MyColumnDescription*/
	Model.Tables[tc.TableName].Columns[tc.ColumnName].Description = tc.Description;

	//Output(tc.TableName + " | " + tc.ColumnName + " | " + tc.Description + " | " + tc.ColumnDescriptionString);
}



/*****************************************************************/
/*************** DAX CODE SKELETON FOR FINAL DIMENSIONS **********/
/*****************************************************************/

var dim_dax_code_skeleton =
@"var _dimension_table = CALCULATETABLE('##dim_table_name##')

var _dimension = 
    SELECTCOLUMNS(
        _dimension_table,
        ""_Inferred"", FALSE(),
        ##dim_column_selects##
    )

var _inferred_bks =
    ##inferred_union_expression##
    

var _inferred_rows =
    SELECTCOLUMNS(
        DISTINCT(_inferred_bks),
        ""_Inferred"", TRUE(),
        ##inferred_column_selects##
    )

var _combined =
    UNION(
        _inferred_rows,
        _dimension           
    )

return
    _combined";

/*****************************************************************/
/*************** CREATE DIMENSION DAX EXPRESSIONS ****************/
/*****************************************************************/

/** Corresponding variables that will hold skeleton parts for dim_dax_code_skeleton **/
var dim_table_name = "";            // ##dim_table_name##
var dim_bk_name = "";               // ##dim_bk_name##
var dim_column_selects = "";        // ##dim_column_selects##
var inferred_union_expression = ""; // ##inferred_union_expression##
var inferred_column_selects = "";   // #inferred_column_selects##
var dim_table_final_name = "";      // Will be used to create the final dimension table name

/** Dax Code Dictionaries & Lists **/
List<Dimension> DimensionTables = new List<Dimension>();
var dimension_table_related_table_expressions = new Dictionary<string, Dictionary<string, List<string>>>();
var dimension_table_keystore_expressions = new Dictionary<string, string>();

/** FETCH DIMENSION TABLES**/
var dimension_tables = Model.Tables.Where( t => t.Name.StartsWith(dim_prefix));

if (!dimension_tables.Any()) {throw new Exception("No dimension tables found!");}

/** GENERATE DAX CODE **/
foreach (var t in dimension_tables) {
    
    /** ##dim_table_name## **/
    dim_table_name = "";
    dim_table_name = t.Name;
    
    /** dim_table_final_name **/
    dim_table_final_name = "";
    var dim_table_name_parts = t.Name.Split("-");    
    if (dim_table_name_parts.Count() != 2) {throw new Exception("Following table must contain 1 number of the character '-' :" + t.Name);} //Check name parts syntax  
    dim_table_final_name = dim_final_prefix + dim_table_name_parts.Last().Trim(); //Store dimension table's final name
    
    /** ##dim_bk_name## **/
    dim_bk_name = "";
    var dimension_bk_column = t.Columns.Where(c => c.Name.StartsWith(bk_prefix));
    if (!dimension_bk_column.Any()) {throw new Exception("No business key column found for: " + t.Name);} //Test BK column exists
    if (dimension_bk_column.Count() > 1) {throw new Exception("More than 1 business key column found for: " + t.Name);} //Test there is only one BK column
    dim_bk_name = dimension_bk_column.First().Name;
    if (dim_bk_name.Replace("_BK -", "").Trim() != dim_table_name_parts.Last().Trim()) {throw new Exception("BK name needs to equal table name after prefixes in table: " + t.Name);} //Test the column name equals table name
    
    
    /** ##dim_column_selects## **/
    dim_column_selects = "";
    foreach (var c in t.Columns) {
        
        var comma = ",";
        var tab = "";
        if (t.Columns.Last() == c) { comma = ""; }
        if (t.Columns.First() != c) { tab = "\t\t"; }
        dim_column_selects += tab + "\"" + c.Name + "\"" + ", '" + dim_table_name + "'[" + c.Name + "]" + comma + "\n";
    
    }
    
    /** ##inferred_union_expression## **/
    inferred_union_expression = "";
	var union_expressions_count = 0;
    var related_tables = 
            Model.Tables
                .Where(
                    local_table => local_table.Columns
                        .Any(c => (c.Name.Split(" | ").First().Equals(dim_bk_name) || c.Name.Split(" / ").First().Equals(dim_bk_name))
                                    && (local_table.Name.StartsWith(fct_prefix) || local_table.Name.StartsWith(bri_prefix))
						)
                );
    
    foreach (var local_table in related_tables) {
        
		var bk_columns_in_scope = local_table.Columns.Where(c => c.Name.StartsWith(dim_bk_name));

		foreach (var local_bk_col in bk_columns_in_scope) {
        
			var local_bk_name = local_bk_col.Name;
			var local_table_name = local_table.Name;
			var comma = ",";
			var tab = "\t\t";
			if (related_tables.Last() == local_table && bk_columns_in_scope.Last() == local_bk_col) { comma = ""; }
			var related_table_string_skeleton = "SELECTCOLUMNS(EXCEPT(VALUES('##related_table_name##'[##local_bk_name##]), VALUES('##dim_table_name##'[##dim_bk_name##])), \"##dim_bk_name##\", '##related_table_name##'[##local_bk_name##])";
			var related_table_string = related_table_string_skeleton
										.Replace("##related_table_name##", local_table_name).Replace("##dim_table_name##", dim_table_name)
										.Replace("##dim_bk_name##", dim_bk_name)
										.Replace("##local_bk_name##", local_bk_name);
			inferred_union_expression += tab + related_table_string + comma + "\n";
			union_expressions_count++;
		}
    }

	if ( !related_tables.Any() ) {

		inferred_union_expression = "TOPN( 0, DATATABLE( \"##dim_bk_name##\", STRING,{ {} } ) ) // No related fact table exists so an empty table was inserted".Replace("##dim_bk_name##", dim_bk_name);
	
	}

    if (union_expressions_count > 1) {

        inferred_union_expression = "UNION(\n" + inferred_union_expression + "\t)";
    
    }
    
    /** ##inferred_column_selects## **/
    inferred_column_selects = "";
    foreach (var c in t.Columns) {
        
        var BooleanType = "FALSE()";
        var DateTimeType = "DATE(9999, 1, 1)";
        var DecimalType = "0";
        var DoubleType = "0";
        var Int64Type = "0";
        var StringType = "-";

        var comma = ",";
        var tab = "";
        var inferred_local_value = "";
    
        if (t.Columns.Last() == c) { comma = ""; }
        if (t.Columns.First() != c) { tab = "\t\t"; }
        
        // Column types switch
        if (c.DataType == DataType.Boolean) { inferred_local_value = BooleanType; }
        if (c.DataType == DataType.DateTime) { inferred_local_value = DateTimeType; }        
        if (c.DataType == DataType.Decimal) { inferred_local_value = DecimalType; }        
        if (c.DataType == DataType.Double) { inferred_local_value = DoubleType; }
        if (c.DataType == DataType.Int64) { inferred_local_value = Int64Type; }
        if (c.DataType == DataType.String) { inferred_local_value = "\"" + StringType + "\""; }

        if (c.Name.Equals(dim_bk_name)) { inferred_local_value = "[" + c.Name + "]"; }
        inferred_column_selects += tab + "\"" + c.Name + "\"" + ", " + inferred_local_value + comma + "\n";
    
    }
      
    //Produce dim dax code
    var dim_dax_code = 
            dim_dax_code_skeleton
                .Replace("##dim_table_name##", dim_table_name)
                .Replace("##dim_bk_name##", dim_bk_name)
                .Replace("##dim_column_selects##", dim_column_selects)
                .Replace("##inferred_column_selects##", inferred_column_selects)
                .Replace("##inferred_union_expression##", inferred_union_expression);
    
	// Add Information regarding Dimension to List
	DimensionTables.Add( new Dimension()
	{
		OriginalDimTable 	= dim_table_name,
		NewDimTable 		= dim_table_final_name,
		TableExpression 	= dim_dax_code
	});

}

/*****************************************************************/
/*************** ADD DIMENSIONS TO MODEL *************************/
/*****************************************************************/

//Loop dimension list and create / update final Dimension Table
foreach (var Dim in DimensionTables) {
    
    var dimension_table_name = Dim.NewDimTable;
    var dimenstion_table_dax_code = Dim.TableExpression;
    
    if (!Model.Tables.Contains(dimension_table_name)) {
        
        Model.AddCalculatedTable(dimension_table_name, "");
    }
	Model.Tables[dimension_table_name].Partitions[dimension_table_name].Expression = dimenstion_table_dax_code;
	Model.Tables[dimension_table_name].TableGroup = dimension_tables_table_group_name;
	Model.Tables[dimension_table_name].SetAnnotation(annotation_tabletype, "Dimension_DAX");

	//Update Column DataTypes for Dimensions - align with raw tables
	//and set key column
	var table_columns = Model.Tables[dimension_table_name].Columns;
	foreach (var col in table_columns) {

		//If column is business key, then set it as key column
		if (col.Name == string.Concat(bk_prefix,dimension_table_name))
			col.IsKey = true;


		//Check for columns not existing in raw table, i.e. created by the procedure => ignore these
		var raw_column = Model.Tables[Dim.OriginalDimTable].Columns.Where(c => c.Name.Equals(col.Name));
		if(raw_column.Count() != 1)
			continue;

		//Force column type from raw table to dimension table
		var old_Datatype = col.DataType;
		var new_Datatype = Model.Tables[Dim.OriginalDimTable].Columns[col.Name].DataType;

		col.DataType = Model.Tables[Dim.OriginalDimTable].Columns[col.Name].DataType;

		//Check if datatype is changed - give output to developer if column type is changed
		if (old_Datatype != new_Datatype)
			Output("Column Type Updated # Dim: " + Dim.NewDimTable + " | Column: " + col.Name + " | DataType: " + old_Datatype + " => " + new_Datatype);

		//Update description if, and only if the source raw table have a description that is not empty
		if (Model.Tables[Dim.OriginalDimTable].Columns[col.Name].Description != "")
			col.Description = Model.Tables[Dim.OriginalDimTable].Columns[col.Name].Description;
	}
}


/*****************************************************************/
/*************** CREATE FACT LAST UPDATED ************************/
/*****************************************************************/

do { //Only one loop, enables us to break out using "break" command
var lastUpdatedTableName = "$ Last Updated";
var lastupdatedColumnName = "last_updated";

//##Check if the last updated table is relevant for current model
	var lastUpdatedTable_exists = Model.Tables.Contains(lastUpdatedTableName);
	var lastUpdatedTable_annotation_tabletype = "";
	
	//Get tabletype annotation
	if(lastUpdatedTable_exists)
		lastUpdatedTable_annotation_tabletype = Model.Tables[lastUpdatedTableName].Annotations[annotation_tabletype].ToString();
	
	//Check if the Last Updated table is already created and not "Fact_DAX" generated.
	if ( lastUpdatedTable_exists && lastUpdatedTable_annotation_tabletype != "Fact_DAX" )
		break;
//##Check done

//Get all tables that have columns like "last_updated"
var tables_with_last_dates = Model.Tables.Where( t => t.Columns.Contains( lastupdatedColumnName ) );

//Define DAX Skeleton of last updated dates
var fct_dax_lastupdated_code_skeleton =
@"SELECTCOLUMNS (
	{
		##table_lastupdated_rows##
	},
	""Table"", [Value1],
	""last_updated"", [Value2]
)
";
//Define empty string for last updated dates rows
var var_table_lastupdated_rows = " ";

//Loop all tables with column last updated
foreach (var tbl in tables_with_last_dates) {
	//Ignore the system table
	if (tbl.Name == lastUpdatedTableName)
		continue;

	/* ¤¤¤ Consider adding additional information like if it is a Fact or Dimension table ¤¤¤ */

	//Update the row string
	var_table_lastupdated_rows = var_table_lastupdated_rows + $"( \"{tbl.Name}\" , MAX( '{tbl.Name}'[{lastupdatedColumnName}] ))\n\t\t,";
}
	
//Cleanup rowstring after last entry
var_table_lastupdated_rows = var_table_lastupdated_rows.Left(var_table_lastupdated_rows.Length - 1);

//Check if table should be created
if (!Model.Tables.Contains(lastUpdatedTableName) && var_table_lastupdated_rows != "")
{
	Model.AddCalculatedTable(lastUpdatedTableName, "");
}

//If table exists and a valid new DAX is generated
if (Model.Tables.Contains(lastUpdatedTableName) && var_table_lastupdated_rows != "")
{
	Model.Tables[lastUpdatedTableName].Partitions[lastUpdatedTableName].Expression = 
		fct_dax_lastupdated_code_skeleton.Replace(
			"##table_lastupdated_rows##", var_table_lastupdated_rows
	);
	Model.Tables[lastUpdatedTableName].TableGroup = "🔢 Fact Tables";
	Model.Tables[lastUpdatedTableName].SetAnnotation(annotation_tabletype, "Fact_DAX");
	Model.Tables[lastUpdatedTableName].IsHidden = true;
}
else
{
	//Throw exeption if all last_updated columns have been removed while the table exists
	throw new Exception("DeltaDAX: Last Updated Fact exists, while no 'last_updated' columns have been identified. => Please correct the error and rerun script.");
}
}
while(false); //Finalize the last updated section




/*****************************************************************/
/*************** CREATE FACT TABLE RELATIONSHIPS *****************/
/*****************************************************************/

var fct_rl_tables = Model.Tables.Where( t => t.Name.StartsWith(fct_prefix));

foreach (var tbl in fct_rl_tables) {
    
    // Generate relations to dimension tables
    var bk_columns = Model.Tables[tbl.Name].Columns.Where(c => c.Name.StartsWith(bk_prefix));
	var duped_bk_columns = bk_columns.Where(c => c.Name.Contains(" | "));
    
	if (duped_bk_columns.Any()) {
	
		tbl.SetAnnotation(has_duped_bks_annotation, "true"); 
	
	} else {

		tbl.SetAnnotation(has_duped_bks_annotation, "false"); 
		
	}
	
    foreach (var col in bk_columns) {
    
        var fct_tbl_name = tbl.Name;
        var fct_col_name = col.Name;
		var split_value = fct_col_name.Contains(" | ") ? " | " : " / ";
        var dim_tbl_name = dim_final_prefix + fct_col_name.Replace(bk_prefix, "").Split(split_value).First().Trim();
        var dim_col_name = fct_col_name.Split(split_value).First().Trim();

		
		if (fct_col_name.Contains("|") && !fct_col_name.Contains(" | ")) {
            throw new Exception($"Column '{fct_col_name}' in table '{fct_tbl_name}' is using '|' (pipe) without spaces around like this: ' | '. ");
        }
	
		if (fct_col_name.Contains("/") && !fct_col_name.Contains(" / ")) {
            throw new Exception($"Column '{fct_col_name}' in table '{fct_tbl_name}' is using '/' (forward slash) without spaces around like this: ' / '. ");
        }
    
        var existing_relations = Model.Relationships.Where(r => r.FromTable.Name == fct_tbl_name && r.FromColumn.Name == fct_col_name);
        
        var from_col = Model.Tables[fct_tbl_name].Columns[fct_col_name];
        
        if (!Model.Tables.Contains(dim_tbl_name)) {
            throw new Exception($"Table '{fct_tbl_name}' is referencing a dimension '{dim_tbl_name}' via the business key '{dim_col_name}' which does not exist.");
        }
        
        var to_col = Model.Tables[dim_tbl_name].Columns[dim_col_name];

		// If no existing relation
        if (!existing_relations.Any()) {
            
            var new_relation = Model.AddRelationship();
            new_relation.FromColumn = from_col;
            new_relation.ToColumn = to_col;

            if (fct_col_name.Contains(split_value) && !fct_col_name.Contains(" *")) {
                new_relation.IsActive = false;
            }
        }

		// If there is an existing relation
		if (existing_relations.Any()) {

            if (fct_col_name.Contains(split_value) && fct_col_name.Contains(" *")) {
                existing_relations.First().IsActive = true;
            }

			if (fct_col_name.Contains(split_value) && !fct_col_name.Contains(" *")) {
                existing_relations.First().IsActive = false;
            }
        }

    }

    // Generate relations for date keys
    var dk_columns = Model.Tables[tbl.Name].Columns.Where(c => c.Name.StartsWith(dk_prefix));
	var duped_dk_columns = dk_columns.Where(c => c.Name.Contains(" | "));
	
	if (duped_dk_columns.Any()) {
	
		tbl.SetAnnotation(has_duped_dks_annotation, "true"); 
	
	} else {

		tbl.SetAnnotation(has_duped_dks_annotation, "false"); 
		
	}

    foreach (var col in dk_columns) {
    
        var fct_tbl_name = tbl.Name;
        var fct_col_name = col.Name;
		var split_value = fct_col_name.Contains(" | ") ? " | " : " / ";
        var dt_tbl_name = dim_final_prefix + fct_col_name.Replace(dk_prefix, "").Split(split_value).First().Trim();

		if (!fct_col_name.Contains(" | ") && !fct_col_name.Contains(" / ")) {
            throw new Exception($"Column '{fct_col_name}' in table '{fct_tbl_name}' must use ' / ' or ' | ' followed by the name of the type of date in question, e.g. Shipping Date, Invoice Date, etc..");
        }
        
		var dt_col_name = "Date";
    
        var existing_relations = Model.Relationships.Where(r => r.FromTable.Name == fct_tbl_name && r.FromColumn.Name == fct_col_name);
        
        var from_col = Model.Tables[fct_tbl_name].Columns[fct_col_name];


        if (!Model.Tables.Contains(dt_tbl_name)) {
            throw new Exception($"Table '{fct_tbl_name}' is referencing a date table '{dt_tbl_name}' via the from date key '{fct_col_name}' which does not exist.");
        }
        
        var to_col = Model.Tables[dt_tbl_name].Columns[dt_col_name];
        
		// If no existing relation
        if (!existing_relations.Any()) {
            
            var new_relation = Model.AddRelationship();
            new_relation.FromColumn = from_col;
            new_relation.ToColumn = to_col;
            
            if (!fct_col_name.Contains(" *")) {
                new_relation.IsActive = false;
            }
        }

		// If there is an existing relation
		if (existing_relations.Any()) {

            if (fct_col_name.Contains(" *")) {
                existing_relations.First().IsActive = true;
            }
			
			if (!fct_col_name.Contains(" *")) {
                existing_relations.First().IsActive = false;
            }
        }

    }

    
}

/*****************************************************************/
/*************** CREATE BRIDGE TABLE RELATIONSHIPS ***************/
/*****************************************************************/

var bri_rl_tables = Model.Tables.Where( t => t.Name.StartsWith(bri_prefix));

foreach (var tbl in bri_rl_tables) {

    var bk_columns = Model.Tables[tbl.Name].Columns.Where(c => c.Name.StartsWith(bk_prefix));
	var bri_tbl_name = tbl.Name;
	var to_dim_bk_name = bk_prefix + bri_tbl_name.Split(" to ").Last().Replace(bri_prefix, "").Trim();
	var from_dim_bk_name = bk_prefix + bri_tbl_name.Split(" to ").First().Replace(bri_prefix, "").Trim();

	if (bk_columns.Count() != 2) {
            throw new Exception($"Table '{bri_tbl_name}' must contain exactly 2 business keys ('_BK - ').");
	}

	if (!bri_tbl_name.Contains(" to ")) {
		throw new Exception($"Table '{bri_tbl_name}' must contain ' to ' so source and target tables can be identified.");
	}	

	if (!bk_columns.Any(c => c.Name == to_dim_bk_name)) {
		throw new Exception($"Table '{bri_tbl_name}' does not contain a name after the ' to ' separator that match a BK in the table.");
	}
	
	if (!bk_columns.Any(c => c.Name == from_dim_bk_name)) {
		throw new Exception($"Table '{bri_tbl_name}' does not contain a name before the ' to ' separator that match a BK in the table.");
	}
    
    foreach (var col in bk_columns) {
            
        var bri_col_name = col.Name;
        var dim_tbl_name = dim_final_prefix + bri_col_name.Replace(bk_prefix, "").Trim();

		var existing_relations = Model.Relationships.Where(r => r.FromTable.Name == bri_tbl_name && r.FromColumn.Name == bri_col_name);
        
        var from_col = Model.Tables[bri_tbl_name].Columns[bri_col_name];
        
        if (!Model.Tables.Contains(dim_tbl_name)) {
            throw new Exception($"Table '{bri_tbl_name}' is referencing a table '{dim_tbl_name}' via the key '{bri_col_name}' which does not exist.");
        }
        
        var to_col = Model.Tables[dim_tbl_name].Columns[bri_col_name];
        
        if (!existing_relations.Any()) {
            
            var new_relation = Model.AddRelationship();
            new_relation.FromColumn = from_col;
            new_relation.ToColumn = to_col;

            if (bri_col_name == to_dim_bk_name) {
                new_relation.CrossFilteringBehavior = CrossFilteringBehavior.BothDirections;
                new_relation.SecurityFilteringBehavior = SecurityFilteringBehavior.BothDirections;
            }
        }

    }
    
}

/*****************************************************************/
/*************** CREATE MANY-TO-MANY RELATIONSHIPS ***************/
/*****************************************************************/

var mm_rl_tables = Model.Tables.Where( t => t.Columns.Any(c => c.Name.StartsWith(mm_prefix) && !t.Name.StartsWith(dim_prefix)));

foreach (var tbl in mm_rl_tables) {
	
	var mm_columns = Model.Tables[tbl.Name].Columns.Where(c => c.Name.StartsWith(mm_prefix));
	
	foreach (var col in mm_columns) {
    
        var mm_tbl_name = tbl.Name;
        var mm_col_name = col.Name;
        var to_tbl_name = mm_col_name.Replace(mm_prefix, "").Split(" / ").First().Trim();
        var to_col_name = mm_col_name.Replace(mm_prefix, "").Replace(" <>", "").Replace(" <", "").Split(" / ").Last().Trim();
    
        var existing_relations = Model.Relationships.Where(r => r.FromTable.Name == mm_tbl_name && r.FromColumn.Name == mm_col_name);
        
        var from_col = Model.Tables[mm_tbl_name].Columns[mm_col_name];
		
		if (!mm_col_name.Contains(" / ")) {
            throw new Exception($"Column '{mm_col_name}' in the table '{mm_tbl_name}' must use ' / ' to define the column that filters this table.");
        }

		if (!mm_col_name.Contains(" <>") && !mm_col_name.Contains(" <")) {
            throw new Exception($"Column '{mm_col_name}' in the table '{mm_tbl_name}' must use ' <>' or ' <' at the end to define the filter direction of the relation.");
        }

        if (!Model.Tables.Contains(to_tbl_name)) {
            throw new Exception($"Table '{mm_tbl_name}' is referencing a table '{to_tbl_name}' via the column '{mm_col_name}' which does not exist.");
        }

		if (!Model.Tables[to_tbl_name].Columns.Contains(to_col_name)) {
            throw new Exception($"Table '{mm_tbl_name}' is referencing a table '{to_tbl_name}' via the column '{mm_col_name}' which does not exist.");
        }
        
        var to_col = Model.Tables[to_tbl_name].Columns[to_col_name];
        
        if (!existing_relations.Any()) {
            
            var new_relation = Model.AddRelationship();
            new_relation.FromColumn = from_col;
            new_relation.ToColumn = to_col;
			new_relation.ToCardinality = RelationshipEndCardinality.Many;
			
			if (mm_col_name.Contains(" <>")) {

				new_relation.CrossFilteringBehavior = CrossFilteringBehavior.BothDirections;

			} else if (mm_col_name.Contains(" <")) {

				new_relation.CrossFilteringBehavior = CrossFilteringBehavior.OneDirection;

			} else {

				throw new Exception($"Filter direction must be specified with either '<' (one direction) or '<>' (both directions) in column: '{mm_tbl_name}'[{mm_col_name}] ");

			}
            
        }

    }

}

/*****************************************************************/
/*************** CREATE USE RELATIONSHIP CALC GROUP **************/
/*****************************************************************/

public string GenerateUseRelationsShipExpression (string fct_name, string fct_col_name, string dim_name, string dim_col_name) {

	var expression = 
			"CALCULATE(" +
			"\n\tSELECTEDMEASURE()," +
			$"\n\tUSERELATIONSHIP('{fct_name}'[{fct_col_name}], '{dim_name}'[{dim_col_name}])" +
			"\n)";
	
	return expression;

}

var use_relationships_calc_group_name = "# Use Relationships";
TabularEditor.TOMWrapper.CalculationGroupTable use_relationships_calc_group;

if (!Model.Tables.Contains(use_relationships_calc_group_name)) {

	use_relationships_calc_group = Model.AddCalculationGroup(use_relationships_calc_group_name);
	use_relationships_calc_group.Columns["Name"].Name = "Perspectives";

} else {

	use_relationships_calc_group = (TabularEditor.TOMWrapper.CalculationGroupTable) Model.Tables[use_relationships_calc_group_name];
	
}

var tables_with_duped_bks = 
	Model.Tables.Where( 
		t => 
		(t.GetAnnotation(has_duped_bks_annotation) == "true" || t.GetAnnotation(has_duped_dks_annotation) == "true") 
		&& t.Name.StartsWith(fct_prefix)
	);

var table_with_duped_bks_names = new List<string>();

foreach (var tbl in tables_with_duped_bks) {

	table_with_duped_bks_names.Add(tbl.Name);

}

foreach (var tbl_name in table_with_duped_bks_names) {

	var duped_bk_cols = Model.Tables[tbl_name].Columns.Where(c => c.Name.Contains(" | ") && (c.Name.StartsWith(bk_prefix) || c.Name.StartsWith(dk_prefix) ));

	foreach (var c in duped_bk_cols) {
		
		var col_name = c.Name;
		
		var dim_name = col_name.Replace(dk_prefix, "").Replace(bk_prefix, "").Split(" | ").First().Trim();
		var dim_col_name = col_name.StartsWith(dk_prefix) ? "Date" : col_name.Split(" | ").First().Trim();
	
		var calc_item_name = dim_name + " | " + col_name.Split(" | ").Last() + " -> " + tbl_name;

		var calc_expression = GenerateUseRelationsShipExpression(
								fct_name: tbl_name,
								fct_col_name: col_name,
								dim_name: dim_name,
								dim_col_name: dim_col_name
							);

		if (!use_relationships_calc_group.CalculationItems.Contains(calc_item_name)) {
			
			var calc_item = use_relationships_calc_group.AddCalculationItem(calc_item_name, calc_expression);
			
			var changed_item = calc_item_name.Contains(" *") 
									? calc_item.Description = $"This is the primary relation and will be used as a default when '{use_relationships_calc_group_name}' is filtered by '{dim_name}'." 
									: calc_item.Description = "";
		
		} 

	}
	

}

/*****************************************************************/
/*************** ADD METRICS TABLES ******************************/
/*****************************************************************/

public string GenerateMsTableDax() {

return
@"DATATABLE(
    ""z_dummy"", STRING,
    {{}}
)
";

}

// Add main metrics table
var ms_tbl_name = ms_tbl_prefix + "Metrics";
if (!Model.Tables.Contains(ms_tbl_name)) {
    Model.AddCalculatedTable(ms_tbl_name, GenerateMsTableDax());
    Model.Tables[ms_tbl_name].Columns["z_dummy"].IsHidden = true;
}
//Set tabletype of metrics tables
Model.Tables[ms_tbl_name].SetAnnotation(annotation_tabletype, "Metrics_DAX");

// Add local metrics table
var ms_local_tbl_name = ms_tbl_prefix + "Metrics (local)";
if (!Model.Tables.Contains(ms_local_tbl_name)) {
    Model.AddCalculatedTable(ms_local_tbl_name, GenerateMsTableDax());
    Model.Tables[ms_local_tbl_name].Columns["z_dummy"].IsHidden = true;
} 
//Set tabletype of metrics tables
Model.Tables[ms_local_tbl_name].SetAnnotation(annotation_tabletype, "Metrics_DAX");

/*****************************************************************/
/*************** ADD BASE MEASURES *******************************/
/*****************************************************************/

// Get all Fact tables
var ms_tables = Model.Tables.Where( t => t.Name.StartsWith(fct_prefix));

foreach (var tbl in ms_tables) {

    var fct_tbl_name = tbl.Name;
    
	// Get Measure Columns ("_MS - ") from FACT
    var ms_cols = Model.Tables[fct_tbl_name].Columns.Where( c => c.Name.StartsWith(ms_prefix));
    
    foreach (var col in ms_cols) {
        
        var fct_col_name = col.Name;
        var clean_fct_tbl_name = fct_tbl_name.Replace(fct_inc_prefix, "").Replace(fct_prefix, "").Trim();
        var ms_name = clean_fct_tbl_name + " | " + col.Name.Replace(ms_prefix, "").Trim();
        var ms_expression = $"Add_expression_for_column ('{fct_tbl_name}'[{fct_col_name}])" ;

        if (!Model.Tables[ms_tbl_name].Measures.Contains(ms_name)) {
            Model.Tables[ms_tbl_name].AddMeasure(ms_name, ms_expression);
            Model.Tables[ms_tbl_name].Measures[ms_name].DisplayFolder = "000. " + clean_fct_tbl_name + "\\00. Base Metrics";
			
			//Add default format string for newly created base measures
			Model.Tables[ms_tbl_name].Measures[ms_name].FormatString = "#,##0";
        }
    
    }   

}

/*****************************************************************/
/*************** ADD DESCRIPTION TO ALL MEASURES *****************/
/*****************************************************************/
/*
01.	Loop all metrics tables
02.	In metrics tables loop all measures
03. Evaluate each measure description / expression
	- Take initial description and expression definition
	- If the expression is long, then truncate the expression by removing a middle part and replacing by ">>>> TRUNCATED <<<<"
	- Search the initial description for prior expression augmentation, i.e. variants on "DeltaDAX: "
	- If found clear prior expression part
04.	Update description by merging "real" description part with the measure expression
	- If the new updated desctiption is the same as initially, then do nothing
	- It there is a change, the description is overwritten


*/
//Find Metrics tables
//var metrics_tables = Model.Tables.Where( t => t.Name.StartsWith(ms_tbl_prefix));		//Allready defined --> Reuse

foreach (var tbl in metrics_tables) {
	foreach (var measure in tbl.Measures) {
		//if (measure.Name != "Forecast Act in FC")
		//	continue;

		var measureDefString = " \nDeltaDAX: ";
		var measureDAX = measureDefString + measure.Expression.TrimEnd().ToString();
		var measureDAX_Length = measureDAX.Length;

		if(measureDAX_Length > 500) {
			var measureDAX_Start = measureDAX.Substring(0,200);
			var measureDAX_End = measureDAX.Substring(measureDAX_Length-280);
			measureDAX = measureDAX_Start + "\n >>>> TRUNCATED <<<<\n"+ measureDAX_End;
		}

		//Get the initial description
		var initialDescription = measure.Description;
		var newMeasureDescription = initialDescription;

        //Remove Dax Measure part of description
        if ( initialDescription.Contains(measureDefString.Replace(" \n", "").Trim()) ) {
			//Set index parts to handle if it might not exist in the string.
			var indexOfDaxPart1 = 9999;
			var indexOfDaxPart2 = 9999;

			//Only update the index if the string is actually found, i.e. not index = -1
			if ( initialDescription.Contains(measureDefString) )
				indexOfDaxPart1 = initialDescription.IndexOf(measureDefString,0);

			if ( initialDescription.Contains(measureDefString.Replace(" \n", "").Trim()) )
				indexOfDaxPart2 = initialDescription.IndexOf(measureDefString.Replace(" \n", "").Trim(),0);

			//Output($"{indexOfDaxPart1} || {indexOfDaxPart2} >>{measureDefString}<< >>{measureDefString.Replace(" \n", "").Trim()}<<");			--DEBUGGING

			//Ensure we take the first instance found
			var indexOfDaxPart = Math.Min(indexOfDaxPart1,indexOfDaxPart2);

			//Remove the DAX part, while retaining the initial decscription
			newMeasureDescription = initialDescription.Substring(0,indexOfDaxPart);
		}

		//Add measure expression to the description
		newMeasureDescription = newMeasureDescription.Trim() + measureDAX;
		newMeasureDescription = newMeasureDescription.Trim();

		//Set measure description
		if (measure.Description != newMeasureDescription)
			measure.Description = newMeasureDescription;
	}
}

/*****************************************************************/
/*************** MOVE TABLES INTO TABLE GROUPS *******************/
/*****************************************************************/

// Move calculation group tables to table group

var calc_gorup_tables = Model.Tables.Where( t => t.GetType() == typeof(TabularEditor.TOMWrapper.CalculationGroupTable));

foreach (var t in calc_gorup_tables) {

	t.TableGroup = calc_group_table_group_name;
	t.SetAnnotation(annotation_tabletype, "CalculationGroup_DAX");

}

// Move tst tables to table group

var tst_tables = Model.Tables.Where( t => t.Name.StartsWith(tst_prefix));

foreach (var t in tst_tables) {

	t.TableGroup = tst_table_group_name;
	t.SetAnnotation(annotation_tabletype, "TestTable_DAX");

}

// Move rtl tables to table group

var rtl_tables = Model.Tables.Where( t => t.Name.StartsWith(rtl_prefix));

foreach (var t in rtl_tables) {

	t.TableGroup = rtl_table_group_name;
	t.SetAnnotation(annotation_tabletype, "ReverseETL_DAX");

}



/*****************************************************************/
/*************** MOVE COLUMNS INTO DISPLAY FOLDERS ***************/
/*****************************************************************/


var time_tables = Model.Tables.Where( t => t.GetAnnotation("DeltaSQL_IsDateTable") == "true");

//Distribute Date Columns into Folders
foreach (var t in time_tables) { 
	foreach ( var c in t.Columns ) {
		
		// Ensure manual sorting is possible
		if ( c.DisplayFolder != string.Empty ) continue;
			
		//If sorted do not update aggregation
		c.SummarizeBy = TabularEditor.TOMWrapper.AggregateFunction.None;

		//Handle base date fields
		if ( c.Name == "Date" || c.Name == "Date int" ) {
			c.DisplayFolder = "01. Date"; continue;
		}

		//Offset
		if ( c.Name.Contains("Offset") ) {
			c.DisplayFolder = "02. Offset"; continue;
		}    

		//Fiscal columns
		if ( c.Name.Contains("Fiscal") ) {
			c.DisplayFolder = "07. Fiscal"; continue;
		}    

		//Month columns
		if ( c.Name.Contains("Month") ) {
			c.DisplayFolder = "04. Month"; continue;
		}

		//Quarter columns
		if ( c.Name.Contains("Quarter") ) {
			c.DisplayFolder = "05. Quarter"; continue;
		}

		//Week columns
		if ( c.Name.Contains("Week") ) {
			c.DisplayFolder = "06. Week"; continue;
		}

		//Year
		if ( c.Name.Contains("Year") ) {
			c.DisplayFolder = "03. Year"; continue;
		}

		//Everything else is put in tech, for manual sorting
		c.DisplayFolder = "99. Tech"; continue; //Default

	}
}

//Set sort by columns
foreach (var t in time_tables) { 
	foreach ( var c in t.Columns ) {
		
		var RegExExpression = new Regex("");

        RegExExpression = new Regex(@"^Month Name.*$");
        if ( RegExExpression.IsMatch( c.Name ) ) {
            c.SortByColumn = t.Columns["Month"];
            continue;
        }
        RegExExpression = new Regex(@"^Month Year.*$");
        if ( RegExExpression.IsMatch( c.Name ) ) {
            c.SortByColumn = t.Columns["Year Month int"];
            continue;
        }

        
        RegExExpression = new Regex(@"^Quarter$");
        if ( RegExExpression.IsMatch( c.Name ) ) {
            c.SortByColumn = t.Columns["Quarter int"];
            continue;
        }

        
        RegExExpression = new Regex(@"^Week Day \(.*$");
        if ( RegExExpression.IsMatch( c.Name ) ) {
            c.SortByColumn = t.Columns["Week Day No"];
            continue;
        }

	}
}



var facts = Model.Tables.Where( t => t.Name.StartsWith(fct_prefix));

foreach (var f in facts) {

    //Loop the Fact table columns
    foreach ( var c in f.Columns ) {
        //Accept manuel sorting
        if ( c.DisplayFolder != string.Empty ) { continue;}
        
        //If sorted do not update aggregation
        c.SummarizeBy = TabularEditor.TOMWrapper.AggregateFunction.None;

        //Key Figures
        if ( c.Name.StartsWith(ms_prefix) ) {
            c.DisplayFolder = "98. Key Figures"; 
            continue;
        }
 
        //Business Keys
        if ( c.Name.StartsWith(dk_prefix) || c.Name.StartsWith(bk_prefix) || c.Name.StartsWith(mm_prefix)) {
            c.DisplayFolder = "99. Tech"; 
            continue;
        }
        
        c.DisplayFolder = "01. Line Items";
    }
}

var Dimensions = Model.Tables.Where( t => t.TableGroup == dimension_tables_table_group_name);

foreach (var d in Dimensions) {

    //Ignore Date Table
    if ( d.GetAnnotation("DeltaSQL_IsDateTable") == "true" ) { continue; }

    //var Dimension = Model.Tables[t.Name].Columns;
    foreach ( var c in d.Columns ) {
		
		var RegExExpression = new Regex("");

        //Accept manuel sorting
        if ( c.DisplayFolder != string.Empty ) { continue;}
        
        //If sorted do not update aggregation
        c.SummarizeBy = TabularEditor.TOMWrapper.AggregateFunction.None;

        //Hierarchies to Hierarchy folder
        RegExExpression = new Regex(@"^H[0-9]{1}_[0-9]{2}$|^.*Hierarchy.*$");
        if ( RegExExpression.IsMatch( c.Name ) ) {
            c.DisplayFolder = "02. Hierarchy"; 
            continue;
        }
        
        //Hierarchy helper/tech columns to Tech subfolder
        RegExExpression = new Regex(@"^H[0-9]{1}_[0-9]{2}_.*$");
        if ( RegExExpression.IsMatch( c.Name ) ) {
            c.DisplayFolder = "99. Tech\\Hierarchy";
            c.IsHidden = true;
            continue;
        } 

        if ( c.Name.StartsWith(bk_prefix) || c.Name.StartsWith(mm_prefix)) {
            c.DisplayFolder = "99. Tech";
			c.IsHidden = true;
            continue;
        }

        if ( c.Name.StartsWith("_Inferred") ) {
            c.DisplayFolder = "99. Tech";
            c.IsHidden = true;
            continue;
        }

        c.DisplayFolder = "03. Attributes";

    }
}


/*****************************************************************/
/*************** CLEAN-UP AREA FOR DELTAFRAMEWORK START **********/
/*****************************************************************/

//Clean-up of legacy Annotations
var AnnotationsTables = Model.Tables.Where( t => (
	t.GetAnnotation("DeltaSQL_10_TableType") != null
	) && t.GetType() != typeof(TabularEditor.TOMWrapper.CalculationGroupTable)		//Calculation Groups are not relevant
);

foreach (var t in AnnotationsTables) {
	t.RemoveAnnotation("DeltaSQL_10_TableType");
}

/*****************************************************************/
/*************** CLEAN-UP AREA FOR DELTAFRAMEWORK END ************/
/*****************************************************************/




/*****************************************************************/
/*************** Object & Function Section START *****************/
/*****************************************************************/

//Class for Dimensions
public class Dimension
{
	public string OriginalDimTable;
	public string NewDimTable;
	public string TableExpression;

	//Example on a function implemented on the object
	public string ExpressionString()
	{
		return OriginalDimTable + " | " + NewDimTable + " | " + TableExpression;
	}

}


//Class for Table Columns
public class TableColumns
{
	public string TableName;
	public string ColumnName;
	public string Description;

	//DeltaSQL Description String Functionality
	private string _ColumnDescriptionString;	
	public string ColumnDescriptionString { //Example: /*DeltaSQL_Desc - MyColumnName: MyColumnDescription*/
		get => _ColumnDescriptionString;
		set {
			_ColumnDescriptionString = value;
			
			//DeltaSQL Description Argument string
			var DeltaSQL_DescPrefix = "DeltaSQL_Desc - ";

			var SplitDescString = value.Replace(DeltaSQL_DescPrefix,"").Split(':');

			//Throw if more than 1 ":" is split on for the description => This leads to inconsistencies
			if (SplitDescString.Count() > 2)
				throw new Exception("Table " + TableName + " | Split of Description: " + value + "failed due to overload - Check the description syntax");  

			//Split the Description String into column and Description based on delimiter ":"
			try {
				//From Description string isolate column and Description
				ColumnName = SplitDescString[0].Trim();
				Description = SplitDescString[1].Trim();
			}
			catch (Exception e) {
				//In case the splitting fails, throw an error. Usually the failure is because of too few argument - we expect 2
				throw new Exception("Table " + TableName + " | Split of Description: " + value.Trim() + "failed - Check the description syntax");  
			}

			//Output(SplitDescString[1].TrimStart());
		}
	}

}



/*** Versioning 																  ***/
/*** MAJOR version when you make incompatible API changes                         ***/
/*** MINOR version when you add functionality in a backward compatible manner     ***/
/*** PATCH version when you make backward compatible bug fixes                    ***/
public class DeltaDAX_Version 
{

	protected string _VersionID;

	private int _MayorVersion;
	private int _MinorVersion;
	private int _PatchVersion;
	
	private string DeltaTool = "DeltaDAX";
	private string _annotation = "DeltaDAX_Version";

	//Singleton pattern
    private DeltaDAX_Version() {}
    private static DeltaDAX_Version instance = null;
    public static DeltaDAX_Version Instance {
        get {
            if (instance == null) {
                instance = new DeltaDAX_Version();
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