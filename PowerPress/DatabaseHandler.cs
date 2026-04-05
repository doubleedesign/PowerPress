using System.Management.Automation;
using MySqlConnector;

namespace PowerPress;

public class DatabaseHandler {
	private readonly LocalSiteConfig config;
	private readonly MySqlConnection connection;
	private readonly Logger logger = new();
	private readonly PowerShellBridge ps = new();
	private readonly UserInput ui = new();

	public DatabaseHandler(LocalSiteConfig config) {
		try {
			this.connection = new MySqlConnection(
				$"Server={config.DbHost};" +
				$"User ID={config.DbUser};" +
				$"Password={config.DbPassword};" +
				$"Port={config.DbPort};"
			);
			this.connection.Open();

			this.config = config;

			// Set the db password as an environment variable so it will be picked up automatically when running commands via CLI
			Environment.SetEnvironmentVariable("MYSQL_PWD", this.config.DbPassword, EnvironmentVariableTarget.Process);
		}
		catch (MySqlException e) {
			if (e.Message.Contains("Unable to connect")) {
				this.logger.ErrorMessage($"{e.Message}. Is MySQL running?");
				Environment.Exit(1);
			}

			this.logger.ErrorMessage(e.Message);
			Environment.Exit(1);
		}
	}


	private bool DbIsEmpty() {
		if (!this.DbExists()) {
			this.logger.WarningMessage($"Database '{this.config.DbName}' does not exist. Skipping check.");
			return false;
		}

		List<Dictionary<string, object>> result = this.ExecuteQuery($"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{this.config.DbName}'");
		if (result.Count > 0 && result[0].ContainsKey("COUNT(*)")) {
			int tableCount = Convert.ToInt32(result[0]["COUNT(*)"]);
			if (tableCount == 0) {
				this.logger.InfoMessage($"Database {this.config.DbName} is empty");
				return true;
			}
		}

		this.logger.InfoMessage($"Database {this.config.DbName} is not empty.");
		return false;
	}

	private bool DbExists() {
		List<Dictionary<string, object>> results = this.ExecuteQuery($"SELECT 1 FROM information_schema.schemata WHERE schema_name = '{this.config.DbName}'");
		if (results.Count > 0) {
			return true;
		}

		return false;
	}

	public void MaybeDropDb(bool definitelyDropIfNotEmpty = false) {
		try {
			bool exists = this.DbExists();
			bool empty = this.DbIsEmpty();
			bool proceed = definitelyDropIfNotEmpty;

			if (!exists) {
				this.logger.InfoMessage($"Database {this.config.DbName} does not exist. Skipping drop.");
				return;
			}

			if (exists && empty) {
				this.logger.SuccessMessage($"Database {this.config.DbName} is empty, skipping drop.");
				return;
			}

			if (!definitelyDropIfNotEmpty) {
				proceed = this.ui.PromptForYesOrNo(
					"Do you want to drop the existing database and create a new one?",
					"Yes, drop the existing database",
					"No, leave the existing database as-is"
				);
			}

			if (proceed) {
				this.ExecuteCommand($"DROP DATABASE {this.config.DbName}");
				if (!this.DbExists()) {
					this.logger.SuccessMessage($"Dropped existing database {this.config.DbName}");
				}
				else {
					this.logger.ErrorMessage($"Problem dropping database {this.config.DbName}");
				}
			}
		}
		catch (RuntimeException e) {
			if (e.Message.Trim().EndsWith("does not exist")) {
				this.logger.InfoMessage($"Database '{this.config.DbName}' does not exist. Skipping drop.");
				return;
			}

			this.logger.ErrorMessage(e.Message);
			Environment.Exit(1);
		}
	}

	public void MaybeCreateDb() {
		if (this.DbExists()) {
			this.logger.WarningMessage($"Database '{this.config.DbName}' already exists. Skipping creation.");
			return;
		}

		this.ExecuteCommand($"CREATE DATABASE IF NOT EXISTS `{this.config.DbName}`");

		if (!this.DbExists()) {
			this.logger.ErrorMessage($"Problem creating database {this.config.DbName}");
			Environment.Exit(1);
		}

		this.logger.SuccessMessage($"Database '{this.config.DbName}' created successfully");
	}

	public void MaybeImportData() {
		if (!this.DbExists()) {
			throw new RuntimeException("Database has not been initialised");
		}

		string pathToSql = this.ui.PromptForText("Enter the path to the .sql file you want to import: ");
		pathToSql = pathToSql.Trim('"');
		if (!File.Exists(pathToSql)) {
			throw new IOException($"SQL file not found: {pathToSql}");
		}

		this.ExecuteCommandViaCli([this.config.DbName, "<", pathToSql]);

		if (this.DbIsEmpty()) {
			this.logger.ErrorMessage("Problem importing database - it is empty.");
			Environment.Exit(1);
		}

		this.logger.SuccessMessage("Database imported successfully");
	}

	private List<Dictionary<string, object>> ExecuteQuery(string query) {
		List<Dictionary<string, object>> results = new();

		try {
			MySqlCommand cmd = new(query, this.connection);
			MySqlDataReader reader = cmd.ExecuteReader();
			while (reader.Read()) {
				Dictionary<string, object> row = new();

				for (int i = 0; i < reader.FieldCount; i++) {
					row[reader.GetName(i)] = reader.GetValue(i);
				}

				results.Add(row);
			}

			reader.Close();
		}
		catch (MySqlException e) {
			this.logger.ErrorMessage(e.Message);
		}

		return results;
	}

	/// <summary>
	///     Execute a non-query SQL command via MySQL Connector.
	/// </summary>
	/// <param name="command"></param>
	/// <param name="exitOnFail"></param>
	private void ExecuteCommand(string command, bool exitOnFail = true) {
		try {
			MySqlCommand cmd = new(command, this.connection);
			cmd.ExecuteNonQuery();
		}
		catch (MySqlException e) {
			this.logger.ErrorMessage(e.Message);
			if (exitOnFail) {
				Environment.Exit(1);
			}
		}
	}

	/// <summary>
	///     Execute a MySQL command via the mysql CLI command.
	/// </summary>
	/// <param name="args"></param>
	private void ExecuteCommandViaCli(string[] args) {
		string[] creds = ["-h", this.config.DbHost, "-P", this.config.DbPort, "-u", this.config.DbUser];
		CommandResult result = this.ps.RunCommand("mysql", creds.Concat(args).ToArray());

		if (!result.Success) {
			this.logger.ErrorMessage(result.Output.First());
			Environment.Exit(1);
		}
	}
}