using System.Collections.Concurrent;
using System.Reflection;
using DevExpress.Xpf.Editors.Helpers;
using DevExpress.XtraScheduler;
using MYOB.AccountRight.SDK;
using MYOB.AccountRight.SDK.Contracts;
using MYOB.AccountRight.SDK.Contracts.Version2;
using MYOB.AccountRight.SDK.Services;

namespace Stratus.AccountRightExport.ViewModels;

public class MainViewModel : ViewModel
{

	#region Fields

	private readonly BackgroundWorker _accountRightBackgroundWorker;

	private readonly BackgroundWorker _databaseBackgroundWorker;

	#endregion

	#region Construction & Finalization

	public MainViewModel()
	{

		_accountRightBackgroundWorker = new()
		{

			WorkerReportsProgress = true,
			WorkerSupportsCancellation = true

		};

		_accountRightBackgroundWorker.DoWork += ReadAccountRightData;
		_accountRightBackgroundWorker.RunWorkerCompleted += CompleteReadAccountRightData;

		_databaseBackgroundWorker = new()
		{

			WorkerReportsProgress = true,
			WorkerSupportsCancellation = true

		};

		_databaseBackgroundWorker.DoWork += WriteAccountRightData;
		_databaseBackgroundWorker.RunWorkerCompleted += CompleteWriteAccountRightData;

		Company = Proxy.Create<CompanyFileModel>();
		Database = Proxy.Create<DatabaseModel>();
		CancelCommand = new DelegateCommand(Cancel, CanCancel);
		ConvertAsyncCommand = new(ConvertAsync, CanConvert);
		GetCompanyFilesAsyncCommand = new(GetCompanyFilesAsync, CanGetCompanyFiles);

	}

	#endregion

	#region Services

	public IDispatcherService DispatcherService => GetService<IDispatcherService>();

	#endregion

	#region Commands

	public ICommand CancelCommand { get; init; }

	public AsyncCommand ConvertAsyncCommand { get; init; }

	public AsyncCommand GetCompanyFilesAsyncCommand { get; init; }

	#endregion

	#region Command Can Exceute

	private bool CanCancel() => ConvertAsyncCommand.IsExecuting;

	private bool CanConvert()
	{

		return
			!string.IsNullOrEmpty(Company.ServerAddress) &&
			!string.IsNullOrEmpty(Company.UserName) &&
			!string.IsNullOrEmpty(Database.SqlServerInstance) &&
			!string.IsNullOrEmpty(Database.DatabaseName) &&
			!string.IsNullOrEmpty(Database.UserID) &&
			!string.IsNullOrEmpty(Database.Password) &&
			SelectedCompany is not null &&
			ServiceListSelectedItems.Count > 0 &&
			!ConvertAsyncCommand.IsExecuting &&
			!_accountRightBackgroundWorker.IsBusy &&
			!_databaseBackgroundWorker.IsBusy;

	}

	private bool CanGetCompanyFiles() =>
		!string.IsNullOrEmpty(Company.ServerAddress) &&
		!ConvertAsyncCommand.IsExecuting;

	#endregion

	#region Command Methods

	public void Cancel() { }

	public async Task ConvertAsync()
	{

		try
		{

			ServiceList.ToList().ForEach(service =>
			{

				service.ReadProgress = 0;
				service.WriteProgress = 0;

			});

			DatabaseViewModel dbViewModel = Proxy.Create<DatabaseViewModel>();
			await dbViewModel.DropAllTables(Database);
			await dbViewModel.DropSchema(Database);
			await dbViewModel.CreateSchema(Database);

			AccountRightReadParametersModel readParameters = new()
			{

				Services = ServiceListSelectedItems.ToList(),
				Database = Database,
				AccountRightCompany = SelectedCompany!,
				CompanyFile = Company

			};

			AccountRightWriteParametersModel writeParameters = new()
			{

				Database = Database

			};

			_databaseBackgroundWorker.WorkerReportsProgress = true;
			_databaseBackgroundWorker.ProgressChanged += UpdateWriteAccountRightData;
			_databaseBackgroundWorker.RunWorkerAsync(writeParameters);

			_accountRightBackgroundWorker.WorkerReportsProgress = true;
			_accountRightBackgroundWorker.ProgressChanged += UpdateReadAccountRightData;
			_accountRightBackgroundWorker.RunWorkerAsync(readParameters);

		}
		catch (Exception ex)
		{

			MessageBoxService.ShowMessage(
				ex.Message,
				"Error",
				MessageButton.OK,
				MessageIcon.Error);

		}

	}

	public async Task GetCompanyFilesAsync()
	{

		await Task.Run(async delegate
		{

			ApiConfiguration config = new(Company.ServerAddress);
			CompanyFileService cfService = new(config);
			List<CompanyFile> cfList = (await cfService.GetRangeAsync()).ToList();

			await DispatcherService.BeginInvoke(new Action(() =>
			{

				CompaniesAvailable.Clear();
				CompaniesAvailable.AddRange(cfList);

			}));

			AccountRightViewModel vm = Proxy.Create<AccountRightViewModel>();
			await DispatcherService.BeginInvoke(new Action(() =>
			{

				ServiceList.Clear();
				ServiceList.AddRange(vm.GetAccountRightServices().OrderBy(e => e.ContractName));
				ServiceListSelectedItems.AddRange(ServiceList.ToList());

			}));

		});

	}

	#endregion

	#region Properties

	public ObservableCollection<AccountRightServiceModel> ServiceList { get; init; } = new();

	public ObservableCollection<AccountRightServiceModel> ServiceListSelectedItems { get; set; } = new();

	public CompanyFileModel Company { get; }

	public DatabaseModel Database { get; }

	public ObservableCollection<CompanyFile> CompaniesAvailable { get; init; } = new();

	public CompanyFile? SelectedCompany
	{

		get => GetProperty(() => SelectedCompany);
		set => SetProperty(() => SelectedCompany, value);

	}

	#endregion

	#region Methods

	private void CompleteReadAccountRightData(object? sender, RunWorkerCompletedEventArgs e)
	{

		WriteQueue.Enqueue(new()
		{
			ItemType = QueueItemType.Stop
		});

	}

	private void CompleteWriteAccountRightData(object? sender, RunWorkerCompletedEventArgs e)
	{

		MessageBoxService.ShowMessage("Extraction Complete", "Information", MessageButton.OK, MessageIcon.Information);
		foreach (AccountRightServiceModel model in ServiceList)
		{

			model.ReadProgress = 0;
			model.WriteProgress = 0;

		}

	}

	private void ReadAccountRightData(object? sender, DoWorkEventArgs e)
	{

		BackgroundWorker? bw = sender as BackgroundWorker;
		AccountRightReadParametersModel? args = e.Argument as AccountRightReadParametersModel;
		if ((bw is not null) && (args is not null))
		{

			args.Services.ForEach(service =>
			{

				Type? contractType = Type
					.GetType($"{service.ContractType!}, MYOB.AccountRight.SDK");

				if (contractType is not null)
				{

					DatabaseViewModel dbViewModel = Proxy.Create<DatabaseViewModel>();
					dbViewModel.CreateTablesForType(
						args.Database,
						contractType).Wait();

					Type? serviceType = Type.GetType($"{service.ServiceType!}, MYOB.AccountRight.SDK");
					if (serviceType is not null)
					{

						ApiConfiguration config = new(args.CompanyFile.ServerAddress);
						CompanyFileCredentials credentials = new(
							args.CompanyFile.UserName,
							args.CompanyFile.Password);

						bool isReadableRange =
							serviceType.GetInterfaces().Any(i =>
								(i.IsGenericType) &&
								(i.GetGenericTypeDefinition() == typeof(IReadableRange<>)));

						if (isReadableRange)
						{

							var serviceObject = Activator.CreateInstance(
								serviceType,
								config,
								null,
								null);

							var contractCollection = Activator.CreateInstance(
								typeof(PagedCollection<>)
									.MakeGenericType(contractType));

							var getRangeMethod = serviceType.GetMethod(
								"GetRange",
								[

									typeof(CompanyFile),
									typeof(string),
									typeof(ICompanyFileCredentials),
									typeof(string)

								]);

							if ((serviceObject is not null) &&
								(contractCollection is not null) &&
								(getRangeMethod is not null))
							{

								var itemsProperty = contractCollection.GetType().GetProperty("Items");
								var countProperty = contractCollection.GetType().GetProperty("Count");
								AccountRightReadUpdateModel updateData = new();
								updateData.Service = service;
								updateData.Service.Contract = contractType;

								if ((itemsProperty is not null) && (countProperty is not null))
								{

									contractCollection = getRangeMethod.Invoke(
										serviceObject,
										[

											args.AccountRightCompany,
											$"$top=1000&$skip={updateData.TotalRecordsExtracted}",
											credentials,
											null

										]);

									updateData.TotalRecordsAvailable = ((long?)countProperty.GetValue(contractCollection)) ?? 0;
									updateData.Records.AddRange(((object[])itemsProperty.GetValue(contractCollection)!).ToList());
									updateData.TotalRecordsExtracted += updateData.Records.Count;

									if (updateData.TotalRecordsAvailable != 0)
									{

										bw.ReportProgress(
											(int)decimal.Floor
											(
												(
													((decimal)updateData.TotalRecordsExtracted) /
													((decimal)updateData.TotalRecordsAvailable)
												) * 100m
											),
											updateData);

									}

									while ((updateData.TotalRecordsExtracted < updateData.TotalRecordsAvailable) && contractCollection?.GetType()?.GetProperty("NextPageLink")?.GetValue(contractCollection) is not null)
									{

										contractCollection = getRangeMethod.Invoke(
											serviceObject,
											[

												args.AccountRightCompany,
												$"$top=1000&$skip={updateData.TotalRecordsExtracted}",
												credentials,
												null

											]);

										AccountRightReadUpdateModel nextUpdateData = new();
										nextUpdateData.Service = service;
										nextUpdateData.TotalRecordsAvailable = updateData.TotalRecordsAvailable;
										nextUpdateData.Records.AddRange(((object[])itemsProperty.GetValue(contractCollection)!).ToList());
										nextUpdateData.TotalRecordsExtracted = updateData.TotalRecordsExtracted + nextUpdateData.Records.Count;
										updateData.TotalRecordsExtracted = nextUpdateData.TotalRecordsExtracted;

										bw.ReportProgress(
											(int)decimal.Floor
											(
												(
													((decimal)nextUpdateData.TotalRecordsExtracted) /
													((decimal)nextUpdateData.TotalRecordsAvailable)
												) * 100m
											),
											nextUpdateData);

									}

								}

							}

						}

					}

				}

			});

		}

	}

	private void UpdateReadAccountRightData(object? sender, ProgressChangedEventArgs e)
	{

		AccountRightReadUpdateModel? model = e.UserState as AccountRightReadUpdateModel;
		if (model is not null)
		{

			model.Service.ReadProgress = e.ProgressPercentage;
			AccountRightQueueItemModel item = new()
			{

				ItemType = QueueItemType.Records,
				TotalRecordsAvailable = model.TotalRecordsAvailable,
				TotalRecordsWritten = model.TotalRecordsExtracted

			};
			
			item.Service = model.Service;
			item.Records.AddRange(model.Records);
			WriteQueue.Enqueue(item);
			
		}

	}

	private void UpdateWriteAccountRightData(object? sender, ProgressChangedEventArgs e)
	{

		AccountRightWriteUpdateModel? model = e.UserState as AccountRightWriteUpdateModel;
		if (model is not null)
		{

			model.Service.WriteProgress = e.ProgressPercentage;

		}

	}

	private void WriteAccountRightData(object? sender, DoWorkEventArgs e)
	{

		BackgroundWorker? bw = sender as BackgroundWorker;
		AccountRightWriteParametersModel? args = e.Argument as AccountRightWriteParametersModel;
		while (bw is not null && !bw.CancellationPending && args is not null)
		{

			while (WriteQueue.Count == 0) Thread.Sleep(1000);
			if (WriteQueue.TryDequeue(out AccountRightQueueItemModel? model))
			{

				if (model.ItemType == QueueItemType.Stop)
					bw.CancelAsync();
				else
				{

					if (model.Service.Contract is null) return;
					DatabaseViewModel dbViewModel = Proxy.Create<DatabaseViewModel>();
					Dictionary<string, DataTable> dataTableList = new();
					Dictionary<string, string> objectMap = new();

					dataTableList.Add(model.Service.ContractName, dbViewModel.CreateDataTableForType2(Database, model.Service.Contract).Result);
					objectMap.Add(model.Service.ContractName, model.Service.ContractName);

					List<string> contractObjectList = new();
					model.Service.Contract.GetProperties().Where(pi => pi.PropertyType.IsGenericType || pi.PropertyType.IsArray).ToList().ForEach(p =>
					{

						if (
							(p.PropertyType.IsArray) ||
							(p.PropertyType.GetGenericTypeDefinition() == typeof(IEnumerable<>)) ||
							(p.PropertyType.GetGenericTypeDefinition() == typeof(IList))
						)
						{

							Type? subType = null;
							if (p.PropertyType.IsArray)
								subType = Type.GetType(p.PropertyType.GetElementType().AssemblyQualifiedName!);
							else
								subType = Type.GetType(p.PropertyType.GetGenericArguments()[0].AssemblyQualifiedName!);

							if (subType is not null)
							{

								objectMap.Add(p.Name, $"{model.Service.ContractName}_{p.Name}");
								DataTable dt = dbViewModel.CreateDataTableForType2(Database, subType).Result;
								dt.TableName = $"{model.Service.ContractName}_{p.Name}";
								dt.Columns.Add("Parent_UID", typeof(Guid));
								dataTableList.Add(dt.TableName, dt);
								contractObjectList.Add(p.Name);

							}

						}

					});

					model.Records.ForEach(record =>
					{

						Dictionary<string, object?> values = dbViewModel.GetSqlValuesForObjectAsync(model.Service.Contract!, record, args.Database).Result;
						string tableName = objectMap[model.Service.ContractName];
						DataTable dt = dataTableList[tableName];
						DataRow dr = dt.NewRow();
						foreach (DataColumn dc in dt.Columns)
						{

							if (dc.ColumnName.Contains("_"))
							{

								string[] fieldNameSplit = dc.ColumnName.Split("_", StringSplitOptions.RemoveEmptyEntries);
								PropertyInfo? pi = record.GetType().GetProperty(fieldNameSplit[0]);
								if (pi is not null)
								{

									if (fieldNameSplit.Length == 2)
									{

										object? fieldValue = pi.GetValue(record)?.GetType()?.GetProperty(fieldNameSplit[1])?.GetValue(pi.GetValue(record));
										if (fieldValue is null)
											dr[dc.ColumnName] = DBNull.Value;
										else
											dr[dc.ColumnName] = fieldValue;

									}
									else
									{

										object? objectFieldValue = pi.GetValue(record)?.GetType()?.GetProperty(fieldNameSplit[1])?.GetValue(pi.GetValue(record));
										object? fieldValue = null;

										if (objectFieldValue is not null)
										{

											for (int i = 2; i < fieldNameSplit.Length - 1; i++)
											{

												objectFieldValue = objectFieldValue?.GetType()?.GetProperty(fieldNameSplit[i])?.GetValue(objectFieldValue);

											}

											fieldValue = objectFieldValue?.GetType()?.GetProperty(fieldNameSplit[fieldNameSplit.Length - 1])?.GetValue(objectFieldValue);

											/*
											object? fieldValue = objectFieldValue.GetType()?.GetProperty(fieldNameSplit[2])?.GetValue(objectFieldValue);
											if (fieldValue is null)
												dr[dc.ColumnName] = DBNull.Value;
											else
												dr[dc.ColumnName] = fieldValue;
											*/
										}

										if (fieldValue is null)
											dr[dc.ColumnName] = DBNull.Value;
										else
											dr[dc.ColumnName] = fieldValue;

									}

								}

							}
							else
							{

								if (values.ContainsKey(dc.ColumnName))
								{

									if (values[dc.ColumnName] is null)
										dr[dc.ColumnName] = DBNull.Value;
									else
										dr[dc.ColumnName] = values[dc.ColumnName];

								}
								else
									dr[dc.ColumnName] = DBNull.Value;

							}

						}

						dt.Rows.Add(dr);

						foreach (string contractObject in contractObjectList)
						{

							object? objectList = record.GetType().GetProperty(contractObject)?.GetValue(record);

							if (objectList is not null)
								foreach (object o in (objectList as IEnumerable<object>))
								{

									if (o is not null)
									{

										Dictionary<string, object?> subObjectValues = dbViewModel.GetSqlValuesForObjectAsync(o.GetType(), o, args.Database).Result;
										string subTableName = objectMap[contractObject];
										DataTable subTable = dataTableList[subTableName];
										DataRow subRow = subTable.NewRow();

										foreach (DataColumn dc in subTable.Columns)
										{

											if (dc.ColumnName == "Parent_UID")
											{

												if (record.GetType().GetProperty("UID") is not null)
													subRow[dc.ColumnName] = record.GetType().GetProperty("UID")?.GetValue(record);

											}
											else
											{

												if (dc.ColumnName.Contains("_"))
												{

													string[] fieldNameSplit = dc.ColumnName.Split("_", StringSplitOptions.RemoveEmptyEntries);
													PropertyInfo? pi = o.GetType().GetProperty(fieldNameSplit[0]);
													if (pi is not null)
													{

														object? fieldValue = pi.GetValue(o)?.GetType()?.GetProperty(fieldNameSplit[1])?.GetValue(pi.GetValue(o));
														if (fieldValue is null)
															subRow[dc.ColumnName] = DBNull.Value;
														else
															subRow[dc.ColumnName] = fieldValue;

													}

												}
												else
												{

													if (subObjectValues.ContainsKey(dc.ColumnName))
													{

														if (subObjectValues[dc.ColumnName] is null)
															subRow[dc.ColumnName] = DBNull.Value;
														else
															subRow[dc.ColumnName] = subObjectValues[dc.ColumnName];

													}
													else
														subRow[dc.ColumnName] = DBNull.Value;

												}

											}

										}

										subTable.Rows.Add(subRow);

									}

								}

						}

					});

					foreach (KeyValuePair<string, DataTable> kvp in dataTableList)
					{

						DataTable dt = dataTableList[kvp.Key];
						using SqlConnection c = new(args.Database.ConnectionString);
						using SqlBulkCopy bulkCopy = new(c);
						bulkCopy.DestinationTableName = $"DC.{dt.TableName}";
						foreach (DataColumn dc in dt.Columns)
						{

							bulkCopy.ColumnMappings.Add(dc.ColumnName, dc.ColumnName);

						}

						c.Open();
						bulkCopy.WriteToServer(dt);
						c.Close();

					}

					AccountRightWriteUpdateModel nextUpdateData = new();
					nextUpdateData.Service = model.Service;
					nextUpdateData.TotalRecordsWritten = model.TotalRecordsWritten;
					nextUpdateData.TotalRecordsAvailable = model.TotalRecordsAvailable;

					bw.ReportProgress(
						(int)decimal.Floor
						(
							(
								((decimal)nextUpdateData.TotalRecordsWritten) /
								((decimal)nextUpdateData.TotalRecordsAvailable)
							) * 100m
						),
						nextUpdateData);

				}

			}

		}

	}

	private ConcurrentQueue<AccountRightQueueItemModel> WriteQueue = new();

	#endregion

}