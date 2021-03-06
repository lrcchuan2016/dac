﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Data.Linq.Mapping;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using RaisingStudio.Data.Common;

namespace RaisingStudio.Data
{
    public class EntityAdapter
    {
        private DataProvider provider;
        private Type entityType;

        private ColumnAttribute[] propertyAttributes;

        private string tableName;
        public string TableName
        {
            get
            {
                return this.tableName;
            }
        }

        private string[] propertyNames;
        public string[] PropertyNames
        {
            get
            {
                return this.propertyNames;
            }
        }
        private Type[] propertyTypes;
        public Type[] PropertyTypes
        {
            get
            {
                return this.propertyTypes;
            }
        }
        private string[] columnNames;
        public string[] ColumnsNames
        {
            get
            {
                return this.columnNames;
            }
        }

        private string[] columnTypes;
        public string[] ColumnTypes
        {
            get
            {
                return this.columnTypes;
            }
        }

        public string[] keyColumns;
        public string[] dbGeneratedColumns;
        public string[] autoSyncOnInsertColumns;

        public EntityAdapter(DataProvider provider, Type entityType)
        {
            this.provider = provider;
            this.entityType = entityType;
            tableName = GetEntityMapping(entityType, out propertyAttributes, out propertyNames, out propertyTypes, out columnNames, out columnTypes);
        }

        public EntityAdapter(DataProvider provider, Type entityType, string tableName, string[] propertyNames, Type[] propertyTypes, string[] columnNames, string[] columnTypes)
        {
            this.provider = provider;
            this.entityType = entityType;
            this.tableName = tableName;
            this.propertyNames = propertyNames;
            this.propertyTypes = propertyTypes;
            this.columnNames = columnNames;
            this.columnTypes = columnTypes;
        }

        #region Mapping
        public static string GetTableName(Type entityType, out bool hasTableAttribute)
        {
            hasTableAttribute = false;
            Type actualEntityType = entityType;
            while ((entityType != null) && (entityType != typeof(object)))
            {
                TableAttribute[] tableAttributes = (TableAttribute[])entityType.GetCustomAttributes(typeof(TableAttribute), false);
                if (tableAttributes.Length > 0)
                {
                    hasTableAttribute = true;
                    string tableName = tableAttributes[0].Name;
                    if (!string.IsNullOrWhiteSpace(tableName))
                    {
                        return tableName;
                    }
                    else
                    {
                        return entityType.Name;
                    }
                }
                entityType = entityType.BaseType;
            }
            if (entityType == typeof(object))
            {
                return actualEntityType.Name;
            }
            else
            {
                return entityType.Name;
            }
        }

        public static string GetEntityMapping(Type entityType, out ColumnAttribute[] propertyAttributes, out string[] propertyNames, out Type[] propertyTypes, out string[] columnNames, out string[] columnTypes)
        {
            bool hasTableAttribute;
            string tableName = GetTableName(entityType, out hasTableAttribute);
            List<PropertyInfo> properties = new List<PropertyInfo>();
            List<ColumnAttribute> propertyColumnAttributes = new List<ColumnAttribute>();
            List<string> dbGeneratedColumnList = new List<string>();
            List<string> autoSyncOnInsertColumnList = new List<string>();
            foreach (var propertyInfo in entityType.GetProperties())
            {
                ColumnAttribute[] columnAttributes = (ColumnAttribute[])propertyInfo.GetCustomAttributes(typeof(ColumnAttribute), false);
                object[] keyAttributes = propertyInfo.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.KeyAttribute), false);
                if (columnAttributes.Length > 0)
                {
                    properties.Add(propertyInfo);
                    ColumnAttribute columnAttribute = columnAttributes[0];
                    propertyColumnAttributes.Add(columnAttribute);
                }
                else if ((!hasTableAttribute) || (keyAttributes != null && keyAttributes.Length > 0))
                {
                    properties.Add(propertyInfo);
                    string dbType;
                    bool canBeNull;
                    GetMappingDbType(propertyInfo, out dbType, out canBeNull);
                    ColumnAttribute columnAttribute = new ColumnAttribute
                    {
                        Name = propertyInfo.Name,
                        IsPrimaryKey = (keyAttributes != null && keyAttributes.Length > 0) || propertyInfo.Name == "Id",
                        IsDbGenerated = propertyInfo.Name == "Id",
                        AutoSync = propertyInfo.Name == "Id" ? AutoSync.OnInsert : AutoSync.Default,
                        DbType = dbType,
                        CanBeNull = canBeNull
                    };
                    propertyColumnAttributes.Add(columnAttribute);
                }
            }
            propertyAttributes = propertyColumnAttributes.ToArray();
            int propertyCount = properties.Count;
            propertyNames = new string[propertyCount];
            propertyTypes = new Type[propertyCount];
            columnNames = new string[propertyCount];
            columnTypes = new string[propertyCount];
            for (int i = 0; i < propertyCount; i++)
            {
                propertyNames[i] = properties[i].Name;
                propertyTypes[i] = properties[i].PropertyType;
                string columnName = propertyColumnAttributes[i].Name;
                columnNames[i] = string.IsNullOrWhiteSpace(columnName) ? propertyNames[i] : columnName;
                columnTypes[i] = propertyColumnAttributes[i].DbType;
            }
            return tableName;
        }

        public static void GetMappingDbType(PropertyInfo propertyInfo, out string dbType, out bool canBeNull)
        {
            dbType = "string";
            canBeNull = false;
            Type propertyType = propertyInfo.PropertyType;
            if (propertyType.IsGenericType)
            {
                if (propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                {
                    canBeNull = true;
                    Type argumentsType = propertyType.GetGenericArguments()[0];
                    if (TypeManager.IsWellKnownDataType(argumentsType))
                    {
                        dbType = TypeManager.GetWellKnownDataTypeName(argumentsType);
                    }
                    else
                    {
                        // TODO:
                    }
                }
            }
            else
            {
                if (TypeManager.IsWellKnownDataType(propertyType))
                {
                    dbType = TypeManager.GetWellKnownDataTypeName(propertyType);
                }
                else
                {
                    // TODO:
                }
            }
        }

        public string[] GetMappingProperties(string[] columnNames, out string[] selectedColumnNames)
        {
            List<string> propertyNameList = new List<string>();
            List<string> selectedColumnList = new List<string>();
            for (int i = 0; i < columnNames.Length; i++)
            {
                for (int j = 0; j < this.columnNames.Length; j++)
                {
                    if (string.Equals(this.columnNames[j], columnNames[i], StringComparison.OrdinalIgnoreCase))
                    {
                        propertyNameList.Add(this.propertyNames[j]);
                        selectedColumnList.Add(columnNames[i]);
                    }
                }
            }
            selectedColumnNames = selectedColumnList.ToArray();
            return propertyNameList.ToArray();
        }

        private object[] GetEntityKeys(object[] primaryKeys, out string[] propertyNames, out Type[] propertyTypes, out string[] columnNames, out string[] columnTypes, out string[] dbGeneratedColumns, out string[] autoSyncOnInsertColumns)
        {
            int propertyCount = propertyAttributes.Length;
            List<string> keyNames = new List<string>();
            List<Type> keyTypes = new List<Type>();
            List<string> keyColumnNames = new List<string>();
            List<string> keyColumnTypes = new List<string>();
            List<string> dbGeneratedColumnList = new List<string>();
            List<string> autoSyncOnInsertColumnList = new List<string>();
            for (int i = 0; i < propertyCount; i++)
            {
                var propertyAttribute = propertyAttributes[i];
                if (propertyAttribute.IsPrimaryKey)
                {
                    keyNames.Add(this.propertyNames[i]);
                    keyTypes.Add(this.propertyTypes[i]);
                    keyColumnNames.Add(this.columnNames[i]);
                    keyColumnTypes.Add(this.columnTypes[i]);
                }
                if (propertyAttribute.IsDbGenerated)
                {
                    dbGeneratedColumnList.Add(this.propertyNames[i]);
                }
                if (propertyAttribute.AutoSync == AutoSync.OnInsert)
                {
                    autoSyncOnInsertColumnList.Add(this.propertyNames[i]);
                }
            }
            propertyNames = keyNames.ToArray();
            propertyTypes = keyTypes.ToArray();
            columnNames = keyColumnNames.ToArray();
            columnTypes = keyColumnTypes.ToArray();
            dbGeneratedColumns = dbGeneratedColumnList.ToArray();
            autoSyncOnInsertColumns = autoSyncOnInsertColumnList.ToArray();
            if ((primaryKeys != null) && (primaryKeys.Length > 0))
            {
                return GetConvertedKeyValues(columnTypes, primaryKeys);
            }
            else
            {
                return primaryKeys;
            }
        }

        private object[] GetEntityKeys<T>(T dataObject, out string[] propertyNames, out Type[] propertyTypes, out string[] columnNames, out string[] columnTypes)
        {
            List<object> primaryKeys = new List<object>();
            Func<object, object>[] converters;
            Func<T, object>[] propertyGetters = GetPropertyGetters<T>(out converters);

            int propertyCount = propertyAttributes.Length;
            List<string> keyNames = new List<string>();
            List<Type> keyTypes = new List<Type>();
            List<string> keyColumnNames = new List<string>();
            List<string> keyColumnTypes = new List<string>();
            for (int i = 0; i < propertyCount; i++)
            {
                if (propertyAttributes[i].IsPrimaryKey)
                {
                    keyNames.Add(this.propertyNames[i]);
                    keyTypes.Add(this.propertyTypes[i]);
                    keyColumnNames.Add(this.columnNames[i]);
                    keyColumnTypes.Add(this.columnTypes[i]);

                    object value = propertyGetters[i](dataObject);
                    if (converters[i] != null)
                    {
                        value = converters[i](value);
                    }
                    primaryKeys.Add(value);
                }
            }
            propertyNames = keyNames.ToArray();
            propertyTypes = keyTypes.ToArray();
            columnNames = keyColumnNames.ToArray();
            columnTypes = keyColumnTypes.ToArray();
            return primaryKeys.ToArray();
        }

        private string GetIdentityColumn()
        {
            string column = ((this.dbGeneratedColumns != null) && (this.dbGeneratedColumns.Length > 0)) ? this.dbGeneratedColumns[0] : ((this.keyColumns != null) && (this.keyColumns.Length > 0)) ? this.keyColumns[0] : ((this.autoSyncOnInsertColumns != null) && (this.autoSyncOnInsertColumns.Length > 0)) ? this.autoSyncOnInsertColumns[0] : null;
            return column;
        }


        public static void GetDataStructure(IDataReader dataReader, out string[] columnNames, out Type[] propertyTypes, out string[] columnTypes)
        {
            // TODO: dataReader.GetSchemaTable()
            int fieldCount = dataReader.FieldCount;
            columnNames = new string[fieldCount];
            propertyTypes = new Type[fieldCount];
            columnTypes = new string[fieldCount];
            for (int i = 0; i < fieldCount; i++)
            {
                columnNames[i] = dataReader.GetName(i);
                columnTypes[i] = dataReader.GetDataTypeName(i);

                Type fieldType = dataReader.GetFieldType(i);
                if (fieldType != typeof(string) && (fieldType != typeof(byte[])))
                {
                    Type nullableType = typeof(Nullable<>);
                    propertyTypes[i] = nullableType.MakeGenericType(fieldType);
                }
                else
                {
                    propertyTypes[i] = fieldType;
                }
            }
        }
        #endregion


        private CommandBuilder defaultCommandBuilder;

        public virtual CommandBuilder GetCommandBuilder(Expression expression, string tableName, string[] propertyNames, Type[] propertyTypes, string[] columnNames, string[] columnTypes)
        {
            if (expression == null && tableName == this.tableName && propertyNames == this.propertyNames)
            {
                if (this.defaultCommandBuilder == null)
                {
                    this.defaultCommandBuilder = InnerGetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
                }
                return this.defaultCommandBuilder;
            }
            else
            {
                CommandBuilder commandBuilder = InnerGetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
                return commandBuilder;
            }
        }

        public CommandBuilder InnerGetCommandBuilder(Expression expression, string tableName, string[] propertyNames, Type[] propertyTypes, string[] columnNames, string[] columnTypes)
        {
            CommandBuilder commandBuilder = this.provider.CreateCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            #region KeyColumns
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            string[] dbGeneratedColumns;
            string[] autoSyncOnInsertColumns;
            GetEntityKeys(null, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes, out dbGeneratedColumns, out autoSyncOnInsertColumns);
            this.keyColumns = keyNames;
            this.dbGeneratedColumns = dbGeneratedColumns;
            this.autoSyncOnInsertColumns = autoSyncOnInsertColumns;
            commandBuilder.KeyColumns = keyNames;
            #endregion
            return commandBuilder;
        }


        public object Execute(Expression expression)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] columns;
            Command command = commandBuilder.GetSelectCommand(out columns);
            return this.provider.Database.ExecuteScalar(command);
        }


        private Action<T, object>[] GetPropertySetters<T>(out Func<object, object>[] converters)
        {
            converters = new Func<object, object>[this.propertyTypes.Length];
            var propertySetters = new Action<T, object>[this.propertyNames.Length];
            for (int i = 0; i < this.propertyNames.Length; i++)
            {
                var instance = Expression.Parameter(typeof(T), "instance");
                var value = Expression.Parameter(typeof(object), "value");
                var convert = Expression.Convert(value, propertyTypes[i]);
                var call = Expression.Call(instance, this.entityType.GetProperty(this.propertyNames[i]).GetSetMethod(), convert);
                var expression = Expression.Lambda<Action<T, object>>(call, instance, value);
                var action = expression.Compile();
                propertySetters[i] = action;
                converters[i] = ConverterManager.Default.GetConverter(this.columnTypes[i], this.propertyTypes[i]);
            }
            return propertySetters;
        }

        private Action<T, object>[] GetPropertySetters<T>(string[] columns, out Func<object, object>[] converters)
        {
            Func<object, object>[] fullConverters;
            var fullPropertySetters = GetPropertySetters<T>(out fullConverters);
            converters = new Func<object, object>[columns.Length];
            var propertySetters = new Action<T, object>[columns.Length];
            for (int i = 0; i < columns.Length; i++)
            {
                int propertyIndex = Array.IndexOf<string>(this.propertyNames, columns[i]);
                propertySetters[i] = fullPropertySetters[propertyIndex];
                converters[i] = fullConverters[propertyIndex];
            }
            return propertySetters;
        }

        private Func<T, object>[] GetPropertyGetters<T>(out Func<object, object>[] converters)
        {
            converters = new Func<object, object>[this.propertyTypes.Length];
            var propertyGetters = new Func<T, object>[this.propertyNames.Length];
            for (int i = 0; i < this.propertyNames.Length; i++)
            {
                var instance = Expression.Parameter(typeof(T), "instance");
                var call = Expression.Call(instance, this.entityType.GetProperty(this.propertyNames[i]).GetGetMethod());
                var convert = Expression.Convert(call, typeof(object));
                var expression = Expression.Lambda<Func<T, object>>(convert, instance);
                var func = expression.Compile();
                propertyGetters[i] = func;
                converters[i] = ConverterManager.Default.GetConverter(this.propertyTypes[i], this.columnTypes[i]);
            }
            return propertyGetters;
        }


        private void SetParameterValues<T>(Command command, T dataObject)
        {
            Func<object, object>[] converters;
            Func<T, object>[] propertyGetters = GetPropertyGetters<T>(out converters);
            for (int i = 0; i < command.Parameters.Count; i++)
            {
                object value = propertyGetters[i](dataObject);
                if (converters[i] != null)
                {
                    value = converters[i](value);
                }
                command.Parameters[i].Value = value;
            }
        }

        private void SetParameterValues<T>(Command command, T dataObject, string[] keyNames)
        {
            Func<object, object>[] converters;
            Func<T, object>[] propertyGetters = GetPropertyGetters<T>(out converters);
            int index = 0;
            for (int i = 0; i < propertyGetters.Length; i++)
            {
                if (!keyNames.Contains(propertyNames[i]))
                {
                    object value = propertyGetters[i](dataObject);
                    if (converters[i] != null)
                    {
                        value = converters[i](value);
                    }
                    command.Parameters[index++].Value = value;
                }
            }
        }

        private void SetParameterValues<T>(Command command, T dataObject, string[] keyNames, string[] columns)
        {
            Func<object, object>[] converters;
            Func<T, object>[] propertyGetters = GetPropertyGetters<T>(out converters);
            int index = 0;
            for (int i = 0; i < propertyGetters.Length; i++)
            {
                if (!keyNames.Contains(propertyNames[i]) && columns.Contains(propertyNames[i]))
                {
                    object value = propertyGetters[i](dataObject);
                    if (converters[i] != null)
                    {
                        value = converters[i](value);
                    }
                    command.Parameters[index++].Value = value;
                }
            }
        }

        private void SetParameterValues<T>(Command command, string[] keyNames, string[] keyColumnTypes, object[] primaryKeys)
        {
            for (int i = 0; i < primaryKeys.Length; i++)
            {
                string keyColumnType = keyColumnTypes[i];
                object keyValue = primaryKeys[i];
                if (keyValue != null)
                {
                    var converter = ConverterManager.Default.GetConverter(keyValue.GetType(), keyColumnType);
                    if (converter != null)
                    {
                        keyValue = converter(keyValue);
                    }
                    command.Parameters[(columnNames.Length - keyNames.Length) + i].Value = keyValue;
                }
            }
        }


        private object[] GetConvertedKeyValues(string[] keyColumnTypes, object[] primaryKeys)
        {
            object[] keyValues = new object[primaryKeys.Length];
            for (int i = 0; i < primaryKeys.Length; i++)
            {
                string keyColumnType = keyColumnTypes[i];
                object keyValue = primaryKeys[i];
                if (keyValue != null)
                {
                    var converter = ConverterManager.Default.GetConverter(keyValue.GetType(), keyColumnType);
                    if (converter != null)
                    {
                        keyValue = converter(keyValue);
                    }
                    keyValues[i] = keyValue;
                }
            }
            return keyValues;
        }


        private object SetPropertyValue<T>(Action<T, object>[] propertySetters, Func<object, object>[] converters, T dataObject, int i, object value)
        {
            try
            {
                if (Convert.IsDBNull(value))
                {
                    value = null;
                }
                if (converters[i] != null)
                {
                    value = converters[i](value);
                }
                propertySetters[i](dataObject, value);
            }
            catch (Exception ex)
            {
                string propertyName = propertyNames[i];
                Type propertyType = propertyTypes[i];
                string message = string.Empty;
                if (value != null)
                {
                    message = string.Format("Could not set the property [{0}] of type [{1}] to value [{2}], the value type is [{3}].", propertyName, typeof(T), value, value.GetType());
                }
                else
                {
                    message = string.Format("Could not set the property [{0}] of type [{1}] to [null].", propertyName, typeof(T));
                }
                throw new ApplicationException(message, ex);
            }
            return value;
        }

        private IEnumerable<T> GetEnumerator<T>(IDataReader dataReader, string[] columnNames, Action<T, object>[] propertySetters, Func<object, object>[] converters) where T : new()
        {
            try
            {
                int fieldCount = propertySetters.Length;
                while (dataReader.Read())
                {
                    T dataObject = new T();

                    for (int i = 0; i < fieldCount; i++)
                    {
                        object value = columnNames == null ? dataReader.GetValue(i) : dataReader[columnNames[i]];
                        value = SetPropertyValue<T>(propertySetters, converters, dataObject, i, value);
                    }

                    yield return dataObject;
                }
            }
            finally
            {
                if (dataReader != null)
                {
                    dataReader.Close();
                }
            }
        }

        public IEnumerable<T> GetEnumerator<T>(IDataReader dataReader, string[] columns = null, string[] columnNames = null) where T : new()
        {
            Func<object, object>[] converters;
            Action<T, object>[] propertySetters = ((columns != null) && (columns.Length > 0)) ? GetPropertySetters<T>(columns, out converters) : GetPropertySetters<T>(out converters);
            return GetEnumerator<T>(dataReader, columnNames, propertySetters, converters);
        }

        public IEnumerable<T> Query<T>(Expression expression) where T : new()
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] columns;
            Command command = commandBuilder.GetSelectCommand(out columns);
            var dataReader = this.provider.Database.ExecuteReader(command);
            return GetEnumerator<T>(dataReader, columns);
        }

        public int Insert<T>(T dataObject)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] columns = this.propertyNames.Except(this.dbGeneratedColumns).ToArray();
            return Insert<T>(dataObject, columns, commandBuilder);
        }

        public int Insert<T>(T dataObject, string[] columns)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            return Insert(dataObject, columns, commandBuilder);
        }

        public int Insert<T>(T dataObject, string[] columns, CommandBuilder commandBuilder)
        {
            Command insertCommand = commandBuilder.GetInsertCommand(columns);
            SetParameterValues<T>(insertCommand, dataObject, new string[] { }, columns);
            string identityColumn = GetIdentityColumn();
            if (!string.IsNullOrEmpty(identityColumn))
            {
                Command selectIdentityCommand = commandBuilder.GetIdentityCommand(identityColumn);
                if (commandBuilder.SupportsInsertSelectIdentity)
                {
                    insertCommand.CommandText = insertCommand.CommandText + "; " + selectIdentityCommand.CommandText;
                    object value = this.provider.Database.ExecuteScalar(insertCommand);

                    Func<object, object>[] converters;
                    Action<T, object>[] propertySetters = GetPropertySetters<T>(new[] { identityColumn }, out converters);
                    value = SetPropertyValue<T>(propertySetters, converters, dataObject, 0, value);

                    int result = Convert.ToInt32(value);
                    return result;
                }
                else
                {
                    bool closeConnection = false;
                    try
                    {
                        System.Data.ConnectionState previousConnectionState = this.provider.Database.Connection.State;
                        if ((this.provider.Database.Connection.State & System.Data.ConnectionState.Open) != System.Data.ConnectionState.Open)
                        {
                            this.provider.Database.Connection.Open();
                            closeConnection = true;
                        }

                        int result = this.provider.Database.ExecuteNonQuery(insertCommand);
                        if (result > 0)
                        {
                            object value = this.provider.Database.ExecuteScalar(selectIdentityCommand);
                            result = Convert.ToInt32(value);

                            Func<object, object>[] converters;
                            Action<T, object>[] propertySetters = GetPropertySetters<T>(new[] { identityColumn }, out converters);
                            value = SetPropertyValue<T>(propertySetters, converters, dataObject, 0, result);

                            return result;
                        }
                        return result;
                    }
                    finally
                    {
                        if (closeConnection)
                        {
                            this.provider.Database.Connection.Close();
                        }
                    }
                }
            }
            else
            {
                int result = this.provider.Database.ExecuteNonQuery(insertCommand);
                return result;
            }
        }

        public int Delete(Expression expression)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command deleteCommand = commandBuilder.GetDeleteCommand();
            return this.provider.Database.ExecuteNonQuery(deleteCommand);
        }

        public int Delete(params object[] primaryKeys)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            string[] dbGeneratedColumns;
            string[] autoSyncOnInsertColumns;
            primaryKeys = GetEntityKeys(primaryKeys, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes, out dbGeneratedColumns, out autoSyncOnInsertColumns);
            Command deleteCommand = commandBuilder.GetDeleteCommand(keyColumnNames, keyColumnTypes, primaryKeys);
            return this.provider.Database.ExecuteNonQuery(deleteCommand);
        }

        public int Delete<T>(T dataObject)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            object[] primaryKeys = GetEntityKeys(dataObject, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes);
            Command deleteCommand = commandBuilder.GetDeleteCommand(keyColumnNames, keyColumnTypes, primaryKeys);
            return this.provider.Database.ExecuteNonQuery(deleteCommand);
        }

        public int Update<T>(T dataObject)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            object[] primaryKeys = GetEntityKeys(dataObject, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes);
            Command updateCommand = commandBuilder.GetUpdateCommand(keyColumnNames, keyColumnTypes, primaryKeys, false);
            SetParameterValues<T>(updateCommand, dataObject, keyNames);
            SetParameterValues<T>(updateCommand, keyNames, keyColumnTypes, primaryKeys);
            return this.provider.Database.ExecuteNonQuery(updateCommand);
        }

        public int Update<T>(object[] primaryKeys, T dataObject)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            string[] dbGeneratedColumns;
            string[] autoSyncOnInsertColumns;
            primaryKeys = GetEntityKeys(primaryKeys, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes, out dbGeneratedColumns, out autoSyncOnInsertColumns);
            Command updateCommand = commandBuilder.GetUpdateCommand(keyColumnNames, keyColumnTypes, primaryKeys, true);
            SetParameterValues<T>(updateCommand, dataObject);
            return this.provider.Database.ExecuteNonQuery(updateCommand);
        }

        public int Update<T>(T dataObject, Expression expression)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command updateCommand = commandBuilder.GetUpdateCommand(null);
            SetParameterValues<T>(updateCommand, dataObject);
            return this.provider.Database.ExecuteNonQuery(updateCommand);
        }

        public T GetEntity<T>(params object[] primaryKeys) where T : new()
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            string[] dbGeneratedColumns;
            string[] autoSyncOnInsertColumns;
            primaryKeys = GetEntityKeys(primaryKeys, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes, out dbGeneratedColumns, out autoSyncOnInsertColumns);
            Command selectCommand = commandBuilder.GetSelectCommand(keyColumnNames, keyColumnTypes, primaryKeys);
            var dataReader = this.provider.Database.ExecuteReader(selectCommand);
            var enumerator = GetEnumerator<T>(dataReader);
            foreach (T t in enumerator)
            {
                return t;
            }
            return default(T);
        }

        public IEnumerable<T> Query<T>(string[] columns, Expression expression) where T : new()
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] selectedColumnNames;
            Command command = commandBuilder.GetSelectCommand(columns, out selectedColumnNames);
            var dataReader = this.provider.Database.ExecuteReader(command);
            return GetEnumerator<T>(dataReader, columns, selectedColumnNames);
        }

        public int Update<T>(T dataObject, string[] columns)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            object[] primaryKeys = GetEntityKeys(dataObject, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes);
            Command updateCommand = commandBuilder.GetUpdateCommand(columns, keyColumnNames, keyColumnTypes, primaryKeys, false);
            SetParameterValues<T>(updateCommand, dataObject, keyNames, columns);
            return this.provider.Database.ExecuteNonQuery(updateCommand);
        }

        public int Update<T>(T dataObject, string[] columns, Expression expression)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command updateCommand = commandBuilder.GetUpdateCommand(columns);
            SetParameterValues<T>(updateCommand, dataObject, new string[] { }, columns);
            return this.provider.Database.ExecuteNonQuery(updateCommand);
        }


        public int GetCount<T>(Expression expression = null)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command command = commandBuilder.GetSelectCountCommand();
            int result = Convert.ToInt32(this.provider.Database.ExecuteScalar(command));
            return result;
        }

        public long GetLongCount<T>(Expression expression = null)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command command = commandBuilder.GetSelectCountCommand();
            long result = Convert.ToInt64(this.provider.Database.ExecuteScalar(command));
            return result;
        }

        private object GetFunctionResult(string function, string column, Expression expression = null)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(expression, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command command = commandBuilder.GetSelectFunctionCommand(function, column);
            object result = this.provider.Database.ExecuteScalar(command);
            return result;
        }

        public object GetSum<T>(string column, Expression expression = null)
        {
            return GetFunctionResult("SUM", column, expression);
        }

        public object GetAvg<T>(string column, Expression expression = null)
        {
            return GetFunctionResult("AVG", column, expression);
        }

        public object GetMin<T>(string column, Expression expression = null)
        {
            return GetFunctionResult("MIN", column, expression);
        }

        public object GetMax<T>(string column, Expression expression = null)
        {
            return GetFunctionResult("MAX", column, expression);
        }


        public bool Exists<T>(T dataObject)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            object[] primaryKeys = GetEntityKeys(dataObject, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes);
            Command command = commandBuilder.GetSelectCountCommand(keyColumnNames, keyColumnTypes, primaryKeys);
            int result = Convert.ToInt32(this.provider.Database.ExecuteScalar(command));
            return result > 0;
        }

        public bool Exists<T>(params object[] primaryKeys)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string[] keyNames;
            Type[] keyTypes;
            string[] keyColumnNames;
            string[] keyColumnTypes;
            string[] dbGeneratedColumns;
            string[] autoSyncOnInsertColumns;
            primaryKeys = GetEntityKeys(primaryKeys, out keyNames, out keyTypes, out keyColumnNames, out keyColumnTypes, out dbGeneratedColumns, out autoSyncOnInsertColumns);
            Command command = commandBuilder.GetSelectCountCommand(keyColumnNames, keyColumnTypes, primaryKeys);
            int result = Convert.ToInt32(this.provider.Database.ExecuteScalar(command));
            return result > 0;
        }


        public Command GetMappingCommand<T>(Command command)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command mappingCommand = commandBuilder.GetMappingCommand<T>(command);
            return mappingCommand;
        }


        public IEnumerable<T> EntityQuery<T>(Command command) where T : new()
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, this.propertyTypes, this.columnNames, this.columnTypes);
            Command mappingCommand = commandBuilder.GetMappingCommand<T>(command);
            var dataReader = this.provider.Database.ExecuteReader(mappingCommand);
            string[] columnNames;
            Type[] propertyTypes;
            string[] columnTypes;
            GetDataStructure(dataReader, out columnNames, out propertyTypes, out columnTypes);
            string[] selectedColumnNames;
            string[] columns = GetMappingProperties(columnNames, out selectedColumnNames);
            return GetEnumerator<T>(dataReader, columns, selectedColumnNames);
        }

        public int EntityNonQuery<T>(Command command)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command mappingCommand = commandBuilder.GetMappingCommand<T>(command);
            return this.provider.Database.ExecuteNonQuery(mappingCommand);
        }

        public object EntityScalar<T>(Command command)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command mappingCommand = commandBuilder.GetMappingCommand<T>(command);
            return this.provider.Database.ExecuteScalar(mappingCommand);
        }

        public IDataReader EntityReader<T>(Command command)
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            Command mappingCommand = commandBuilder.GetMappingCommand<T>(command);
            var dataReader = this.provider.Database.ExecuteReader(mappingCommand);
            return dataReader;
        }


        public object GetIdentity<T>()
        {
            CommandBuilder commandBuilder = GetCommandBuilder(null, tableName, propertyNames, propertyTypes, columnNames, columnTypes);
            string identityColumn = GetIdentityColumn();
            Command selectIdentityCommand = commandBuilder.GetIdentityCommand(identityColumn);
            return this.provider.Database.ExecuteScalar(selectIdentityCommand);
        }
    }
}
