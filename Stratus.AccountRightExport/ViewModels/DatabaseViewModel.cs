namespace Stratus.AccountRightExport.ViewModels;

public class DatabaseViewModel : ViewModel
{

	#region Methods

	public async Task CreateSchema(DatabaseModel model)
	{

		string commandText = @"CREATE SCHEMA [DC];";
		await ExecuteCommandAsync(model, commandText);

	}
	
	public async Task<DataTable> CreateDataTableForType2(DatabaseModel model, Type type)
	{

		DataTable dt = new(type.Name);
		Dictionary<string, string> tableFields = await GetSqlFieldsForType(type, model);

		tableFields.ToList().ForEach(kvp =>
		{

			DataColumn column = new(kvp.Key);
			column.AllowDBNull = true;
			Type columnType = typeof(string);
			switch (kvp.Value)
			{

				case "BIT":
					columnType = typeof(bool);
					break;

				case "DATETIME":
					columnType = typeof(DateTime);
					break;

				case "FLOAT":
					columnType = typeof(float);
					break;

				case "UNIQUEIDENTIFIER":
					columnType = typeof(Guid);
					break;

				case "INT":
					columnType = typeof(int);
					break;

				case "BIGINT":
					columnType = typeof(long);
					break;

				default:
					columnType = typeof(string);
					break;

			}

			column.DataType = columnType;
			dt.Columns.Add(column);

		});

		return dt;

	}

	public async Task CreateTablesForType(DatabaseModel model, Type type, string prefix = "", string name = "")
	{

		Dictionary<string, string> tableFields = await GetSqlFieldsForType(type, model);

		if (tableFields.Count > 0)
		{

			StringBuilder sb = new();
			if (prefix == string.Empty)
			{

				sb.Append($"IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'DC' AND TABLE_NAME = '{type.Name}') CREATE TABLE [DC].[{type.Name}](");

			}
			else
			{

				sb.Append($"IF NOT EXISTS(SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'DC' AND TABLE_NAME = '{prefix}_{name}') CREATE TABLE [DC].[{prefix}_{name}](");
				tableFields.Add("Parent_UID", "UNIQUEIDENTIFIER");

			}

			tableFields.Keys.ToList().ForEach(key =>
			{

				sb.Append($"[{key}] {tableFields[key]}, ");

			});

			int lastCommaIndex = sb.ToString().LastIndexOf(",");
			sb[lastCommaIndex] = ')';

			await ExecuteCommandAsync(model, sb.ToString());

		}

	}

	public async Task DropAllTables(DatabaseModel model)
	{

		string commandText = @"
				DECLARE @ObjectName NVARCHAR(255);
				DECLARE TableCursor CURSOR FOR
				SELECT
					Name
				FROM
					sys.objects
				WHERE
					(SCHEMA_NAME([schema_id]) = 'DC');

				OPEN TableCursor;
				FETCH NEXT FROM TableCursor INTO @ObjectName;
				WHILE @@FETCH_STATUS = 0
				BEGIN
	
					EXEC(N'DROP TABLE [DC].[' + @ObjectName + N']');
					FETCH NEXT FROM TableCursor INTO @ObjectName;

				END

				CLOSE TableCursor;
				DEALLOCATE TableCursor;";

		await ExecuteCommandAsync(model, commandText);

	}

	public async Task DropSchema(DatabaseModel model)
	{

		string commandText = @"
				IF EXISTS(SELECT * FROM sys.schemas WHERE name = N'DC')
				BEGIN

					DROP SCHEMA[DC];

				END";

		await ExecuteCommandAsync(model, commandText);

	}

	private async Task ExecuteCommandAsync(DatabaseModel model, string commandText)
	{

		using (SqlConnection connection = new(model.ConnectionString))
		{

			using (SqlCommand command = new(commandText, connection))
			{

				try
				{

					await connection.OpenAsync();
					await command.ExecuteNonQueryAsync();

				}
				finally
				{

					if (connection.State == ConnectionState.Connecting)
						await connection.CloseAsync();

				}

			}

		}

	}

	private async Task<Dictionary<string, string>> GetSqlFieldsForType(Type type, DatabaseModel model)
	{

		await Task.Delay(0);
		Dictionary<string, string> typeFields = new();
		type.GetProperties().ToList().ForEach(async pi =>
		{

			Type? propertyType =
				Nullable.GetUnderlyingType(pi.PropertyType) ?? 
				pi.PropertyType;

			string? typeName = null;

			if (pi.PropertyType.IsEnum)
				typeName = "NVARCHAR(255)";
			else if ((pi.PropertyType.IsClass) && (pi.PropertyType.FullName is not null) && (pi.PropertyType.FullName.Contains("MYOB")) && (!pi.PropertyType.IsArray))
			{

				Dictionary<string, string> subTypeFields = await GetSqlFieldsForType(pi.PropertyType, model);
				subTypeFields.Keys.ToList().ForEach(key =>
				{

					typeFields.Add($"{pi.Name}_{key}", subTypeFields[key]);

				});

			}
			else if (propertyType.IsGenericType)
			{

				if (
					(propertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
					(propertyType.GetGenericTypeDefinition() == typeof(IList))
				)

				await CreateTablesForType(
					model, 
					Type.GetType(propertyType.GetGenericArguments()[0].AssemblyQualifiedName!)!, 
					type.Name,
					pi.Name);

			}
			else
			{

				if (propertyType.IsArray)
				{

					await CreateTablesForType(
						model,
						pi.PropertyType.GetElementType(),
						type.Name,
						pi.Name);

				}
				else
				{

					if (propertyType is not null)
					{

						if (propertyType == typeof(bool)) typeName = "BIT";
						if (propertyType == typeof(DateTime)) typeName = "DATETIME";
						if (propertyType == typeof(decimal)) typeName = "FLOAT";
						if (propertyType == typeof(double)) typeName = "FLOAT";
						if (propertyType == typeof(float)) typeName = "FLOAT";
						if (propertyType == typeof(Guid)) typeName = "UNIQUEIDENTIFIER";
						if (propertyType == typeof(int)) typeName = "INT";
						if (propertyType == typeof(long)) typeName = "BIGINT";
						if (propertyType == typeof(string)) typeName = "NVARCHAR(MAX)";

					}

				}

			}

			if (typeName is not null)
				typeFields.Add(pi.Name, typeName);

		});

		return typeFields;

	}

	public async Task<Dictionary<string, object?>> GetSqlValuesForObjectAsync(Type type, object dataObject, DatabaseModel model, string prefix = "")
	{

		await Task.Delay(0);
		Dictionary<string, object?> objectFields = new();
		type.GetProperties().ToList().ForEach(async pi =>
		{

			Type? propertyType =
				Nullable.GetUnderlyingType(pi.PropertyType) ??
				pi.PropertyType;

			object? objectValue = null;

			if (pi.PropertyType.IsEnum && dataObject is not null)
				objectValue = pi.GetValue(dataObject);
			else if ((pi.PropertyType.IsClass) && (pi.PropertyType.FullName is not null) && (pi.PropertyType.FullName.Contains("MYOB")) && (!pi.PropertyType.IsArray))
			{

				if (dataObject is not null)
				{

					Dictionary<string, object?> subObjectValues = await GetSqlValuesForObjectAsync(pi.PropertyType, pi.GetValue(dataObject)!, model);
					subObjectValues.Keys.ToList().ForEach(key =>
					{

						objectFields.Add($"{pi.Name}_{key}", subObjectValues[key]);

					});

				}
				
			}
			else if (propertyType.IsGenericType || propertyType.IsArray)
			{
			}
			else
			{

				if (dataObject is not null)
					objectValue = pi.GetValue(dataObject);

			}

			if (objectValue is not null)
				objectFields.Add(pi.Name, objectValue);

		});

		return objectFields;

	}

	#endregion

}