﻿#region License
/*
 * NReco Data library (http://www.nrecosite.com/)
 * Copyright 2016 Vitaliy Fedorchenko
 * Distributed under the MIT license
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
using System.Data;

namespace NReco.Data {

	/// <summary>
	/// Data adapter between database and application data models. Implements select, insert, update and delete operations.
	/// </summary>
	public partial class DbDataAdapter {

		/// <summary>
		/// Gets <see cref="IDbConnection"/> associated with this data adapter.
		/// </summary>
		public IDbConnection Connection { get; private set; }

		/// <summary>
		/// Gets <see cref="IDbCommandBuilder"/> associated with this data adapter.
		/// </summary>
		public IDbCommandBuilder CommandBuilder { get; private set; }

		/// <summary>
		/// Gets or sets <see cref="IDbTransaction"/> initiated for the <see cref="Connection"/>.
		/// </summary>
		public IDbTransaction Transaction { get; set; }

		/// <summary>
		/// Gets or sets flag that determines whether query record offset is applied during reading query results.
		/// </summary>
		public bool ApplyOffset { get; set; } = true;

		/// <summary>
		/// Initializes a new instance of the DbDataAdapter.
		/// </summary>
		/// <param name="connection">database connection instance</param>
		/// <param name="cmdBuilder">command builder instance</param>
		public DbDataAdapter(IDbConnection connection, IDbCommandBuilder cmdBuilder) {
			Connection = connection;
			CommandBuilder = cmdBuilder;
		}

		private void InitCmd(IDbCommand cmd) {
			cmd.Connection = Connection;
			if (Transaction!=null)
				cmd.Transaction = Transaction;
		}

		/// <summary>
		/// Returns prepared select query. 
		/// </summary>
		/// <param name="q">query to execute</param>
		/// <returns>prepared select query</returns>
		public SelectQuery Select(Query q) {
			var selectCmd = CommandBuilder.GetSelectCommand(q);
			InitCmd(selectCmd);
			return new SelectQuery(this, selectCmd, q, null);
		}

		/// <summary>
		/// Returns prepared select query with POCO-model fields mapping configuration. 
		/// </summary>
		/// <param name="q">query to execute</param>
		/// <returns>prepared select query</returns>
		public SelectQuery Select(Query q, IDictionary<string,string> fieldToPropertyMap) {
			var selectCmd = CommandBuilder.GetSelectCommand(q);
			InitCmd(selectCmd);
			return new SelectQuery(this, selectCmd, q, fieldToPropertyMap);
		}

		int InsertInternal(string tableName, IEnumerable<KeyValuePair<string,IQueryValue>> data) {
			var insertCmd = CommandBuilder.GetInsertCommand(tableName, data);
			InitCmd(insertCmd);
			return ExecuteNonQuery(insertCmd);
		}
		
		/// <summary>
		/// Executes INSERT statement generated by specified table name and dictionary values.
		/// </summary>
		/// <param name="tableName">table name</param>
		/// <param name="data">dictonary with new record data (column -> value)</param>
		/// <returns>Number of inserted data records.</returns>
		public int Insert(string tableName, IDictionary<string,object> data) {
			return InsertInternal(tableName, DataHelper.GetChangeset(data) );
		}

		/// <summary>
		/// Executes INSERT statement generated by specified table name and POCO model.
		/// </summary>
		/// <param name="tableName">table name</param>
		/// <param name="pocoModel">POCO model with public properties that match table columns.</param>
		/// <returns>Number of inserted data records.</returns>
		public int Insert(string tableName, object pocoModel) {
			return InsertInternal(tableName, DataHelper.GetChangeset( pocoModel, null) );
		}

		/// <summary>
		/// Executes INSERT statement generated for specified table and POCO model.
		/// </summary>
		/// <param name="tableName">table name</param>
		/// <param name="pocoModel">POCO model with public properties.</param>
		/// <param name="propertyToFieldMap">POCO model property -> column name map</param>
		/// <returns>Number of inserted data records.</returns>
		public int Insert(string tableName, object pocoModel, IDictionary<string,string> propertyToFieldMap) {
			return InsertInternal(tableName, DataHelper.GetChangeset( pocoModel, propertyToFieldMap) );
		}

		
		int UpdateInternal(Query q, IEnumerable<KeyValuePair<string,IQueryValue>> data) {
			var updateCmd = CommandBuilder.GetUpdateCommand(q, data);
			InitCmd(updateCmd);
			return ExecuteNonQuery(updateCmd);
		}
		
		/// <summary>
		/// Executes UPDATE statement generated by specified <see cref="Query"/> and dictionary values.
		/// </summary>
		/// <param name="q">query that determines data records to update.</param>
		/// <param name="data">dictonary with changeset data (column -> value)</param>
		/// <returns>Number of updated data records.</returns>
		public int Update(Query q, IDictionary<string,object> data) {
			return UpdateInternal(q, DataHelper.GetChangeset(data) );
		}

		/// <summary>
		/// Executes UPDATE statement generated by specified <see cref="Query"/> and POCO model.
		/// </summary>
		/// <param name="q">query that determines data records to update.</param>
		/// <param name="pocoModel">POCO model with public properties that match table columns.</param>
		/// <returns>Number of updated data records.</returns>
		public int Update(Query q, object pocoModel) {
			return UpdateInternal(q, DataHelper.GetChangeset( pocoModel, null) );
		}

		/// <summary>
		/// Executes UPDATE statement generated by specified <see cref="Query"/> and POCO model.
		/// </summary>
		/// <param name="q">query that determines data records to update.</param>
		/// <param name="pocoModel">POCO model with public properties.</param>
		/// <param name="propertyToFieldMap">POCO model property -> column name map</param>
		/// <returns>Number of updated data records.</returns>
		/// <remarks>
		/// If <paramref name="propertyToFieldMap"/> is specified, only properties from the map are used for UPDATE.
		/// </remarks>
		public int Update(Query q, object pocoModel, IDictionary<string,string> propertyToFieldMap) {
			return UpdateInternal(q, DataHelper.GetChangeset( pocoModel, propertyToFieldMap) );
		}

		void EnsurePrimaryKey(RecordSet recordSet) {
			if (recordSet.PrimaryKey==null || recordSet.PrimaryKey.Length==0)
				throw new NotSupportedException("Update operation can be performed only for RecordSet with PrimaryKey");
		}

		/// <summary>
		/// Calls the respective INSERT, UPDATE, or DELETE statements for each inserted, updated, or deleted row in the specified <see cref="RecordSet"/>.
		/// </summary>
		/// <param name="tableName">The name of the source table.</param>
		/// <param name="recordSet"><see cref="RecordSet"/> to use to update the data source.</param>
		/// <returns>The number of rows successfully updated.</returns>
		/// <remarks>
		/// <para><see cref="RecordSet.PrimaryKey"/> should be set before calling <see cref="DbDataAdapter.Update(string, RecordSet)"/>.</para>
		/// When an application calls the Update method, <see cref="DbDataAdapter"/> examines the <see cref="RecordSet.Row.State"/> property,
		/// and executes the required INSERT, UPDATE, or DELETE statements iteratively for each row (based on the order of rows in RecordSet).
		/// </remarks>
		public int Update(string tableName, RecordSet recordSet) {
			EnsurePrimaryKey(recordSet);
			var rsAdapter = new RecordSetAdapter(this, tableName, recordSet);
			var affected = rsAdapter.Update();
			recordSet.AcceptChanges();
			return affected;
		}

		/// <summary>
		/// An asynchronous version of <see cref="Update(string, RecordSet)"/> that calls the respective INSERT, UPDATE, or DELETE statements 
		/// for each added, updated, or deleted row in the specified <see cref="RecordSet"/>.
		/// </summary>
		/// <param name="tableName">The name of the source table.</param>
		/// <param name="recordSet"><see cref="RecordSet"/> to use to update the data source.</param>
		/// <param name="cancel">The cancellation instruction.</param>
		/// <returns>The number of rows successfully updated.</returns>
		public Task<int> UpdateAsync(string tableName, RecordSet recordSet) {
			return UpdateAsync(tableName, recordSet, CancellationToken.None); 
		}

		/// <summary>
		/// An asynchronous version of <see cref="Update(string, RecordSet)"/> that calls the respective INSERT, UPDATE, or DELETE statements 
		/// for each added, updated, or deleted row in the specified <see cref="RecordSet"/>.
		/// </summary>
		/// <param name="tableName">The name of the source table.</param>
		/// <param name="recordSet"><see cref="RecordSet"/> to use to update the data source.</param>
		/// <param name="cancel">The cancellation instruction.</param>
		/// <returns>The number of rows successfully updated.</returns>
		public async Task<int> UpdateAsync(string tableName, RecordSet recordSet, CancellationToken cancel) {
			EnsurePrimaryKey(recordSet);
			var rsAdapter = new RecordSetAdapter(this, tableName, recordSet);
			var affected = await rsAdapter.UpdateAsync(cancel).ConfigureAwait(false);
			recordSet.AcceptChanges();
			return affected;		
		}


		/// <summary>
		/// Executes DELETE statement generated by specified <see cref="Query"/>.
		/// </summary>
		/// <param name="q">query that determines data records to delete.</param>
		/// <returns>Number of actually deleted records.</returns>
		public int Delete(Query q) {
			var deleteCmd = CommandBuilder.GetDeleteCommand(q);
			InitCmd(deleteCmd);
			return ExecuteNonQuery(deleteCmd);
		}

		/// <summary>
		/// Asynchronously executes DELETE statement generated by specified <see cref="Query"/>.
		/// </summary>
		public Task<int> DeleteAsync(Query q) {
			return DeleteAsync(q, CancellationToken.None);
		}

		/// <summary>
		/// Asynchronously executes DELETE statement generated by specified <see cref="Query"/>.
		/// </summary>
		public async Task<int> DeleteAsync(Query q, CancellationToken cancel) {
			var deleteCmd = CommandBuilder.GetDeleteCommand(q);
			InitCmd(deleteCmd);
			
			var isClosedConn = deleteCmd.Connection.State == ConnectionState.Closed;
			if (isClosedConn) {
				await deleteCmd.Connection.OpenAsync(cancel).ConfigureAwait(false);
			}
			int affected = 0;
			try {
				affected = await deleteCmd.ExecuteNonQueryAsync(cancel).ConfigureAwait(false);	
			} finally {
				if (isClosedConn)
					deleteCmd.Connection.Close();
			}
			return affected;
		}


		private int ExecuteNonQuery(IDbCommand cmd) {
			int affectedRecords = 0;
			DataHelper.EnsureConnectionOpen(Connection, () => {
				affectedRecords = cmd.ExecuteNonQuery();
			});
			return affectedRecords;
		}

	}
}
