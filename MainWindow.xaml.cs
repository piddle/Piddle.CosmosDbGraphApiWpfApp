using System;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Graphs;
using System.IO;
using Newtonsoft.Json;
using System.Windows;

namespace Piddle.CosmosDbGraphApiWpfApp
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private DocumentClient client;

		public MainWindow()
		{
			InitializeComponent();

			Dump("Output of Queries.txt:" + Environment.NewLine + File.ReadAllText("..\\..\\Queries.txt"));
		}

		private void Connect_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this.client?.Dispose();

				this.client = new DocumentClient(
					new Uri("https://localhost:8081"),
					"C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==",
					new ConnectionPolicy { ConnectionMode = ConnectionMode.Direct, ConnectionProtocol = Protocol.Https });

				this.client.OpenAsync().Wait();
			}
			catch (Exception ex)
			{
				Dump($"Exception:\r\n{ex}");
			}
		}

		private void CreateDb_Click(object sender, RoutedEventArgs e)
		{
			TopLevelExceptionHandler(() =>
			{
				//var response = client.CreateDatabaseIfNotExistsAsync
				var response = this.client.CreateDatabaseAsync(
					new Database { Id = "graphdb" })
					.Result;

				ShowResponse(response);
			});
		}

		private void DeleteDb_Click(object sender, RoutedEventArgs e)
		{
			TopLevelExceptionHandler(() =>
			{
				var response = this.client.DeleteDatabaseAsync(
					UriFactory.CreateDatabaseUri("graphdb"))
					.Result;

				ShowResponse(response);
			});
		}

		private void CreateGraph_Click(object sender, RoutedEventArgs e)
		{
			TopLevelExceptionHandler(() =>
			{
				//var response = client.CreateDocumentCollectionIfNotExistsAsync(
				var response = this.client.CreateDocumentCollectionAsync(
					UriFactory.CreateDatabaseUri("graphdb"),
					new DocumentCollection { Id = "graph" },
					new RequestOptions { OfferThroughput = 1000 })
					.Result;

				ShowResponse(response);
			});
		}

		private void DeleteGraph_Click(object sender, RoutedEventArgs e)
		{
			TopLevelExceptionHandler(() =>
			{
				var response = this.client.DeleteDocumentCollectionAsync(
					UriFactory.CreateDocumentCollectionUri("graphdb", "graph"))
					.Result;

				ShowResponse(response);
			});
		}

		private void CreateTree_Click(object sender, RoutedEventArgs e)
		{
			this.id = 1;

			TopLevelExceptionHandler(() =>
			{
				// - Root
				//   |- Folder1
				//           |- Folder11
				//					|- Location111
				//           |- Folder12
				//   |- Folder2
				//           |- Folder21
				//           |- Folder22
				//                  |- Folder221
				//							|- Location2211

				var rootId = CreateNode(null, "FolderNode", "Root", true, 0);
				var folder1Id = CreateNode(rootId, "FolderNode", "Folder1", false, 1);
				var folder11Id = CreateNode(folder1Id, "FolderNode", "Folder11", false, 11);
				var folder12Id = CreateNode(folder1Id, "FolderNode", "Folder12", true, 12);
				var folder2Id = CreateNode(rootId, "FolderNode", "Folder2", false, 2);
				var folder21Id = CreateNode(folder2Id, "FolderNode", "Folder21", true, 21);
				var folder22Id = CreateNode(folder2Id, "FolderNode", "Folder22", false, 22);
				var folder221Id = CreateNode(folder22Id, "FolderNode", "Folder221", false, 221);

				var location111Id = CreateNode(folder11Id, "LocationNode", "Location111", true, 111);
				var location2211Id = CreateNode(folder221Id, "LocationNode", "Location2211", false, 2211);
			});

			//            RunGremlinQuery(@"
			//g.addV('FolderNode').as('root').property('id', '1').property('name', 'Root').property('booly', true).property('numby', 0)
			//.addV('FolderNode').as('child').property('id', '3').property('name', 'Folder1').property('booly', false).property('numby', 1)
			//.addE('parent').to(g.V('1')).as('link').property('id', '4').property('name', 'symmy')
			//.select('root').valueMap(true).as('rootValues')
			//.select('child').valueMap(true).as('childValues')
			//.select('link').valueMap(true).as('linkValues')
			//.select('rootValues','childValues','linkValues')
			//            ");
		}

		private void DeleteTree_Click(object sender, RoutedEventArgs e)
		{
			// drop all - g.V().drop()
			// g.V('1','3').drop()
			// Long way: g.V('1','3').as('verticies').or(bothE().drop(), drop())
			// No need to delete edges between verticies.
			RunGremlinQuery(@"
g.V().drop()
			");
		}

		private ResourceResponse<DocumentCollection> GetGraph()
		{
			return this.client.ReadDocumentCollectionAsync(
				UriFactory.CreateDocumentCollectionUri("graphdb", "graph"),
				new RequestOptions { OfferThroughput = 1000 })
				.Result;
		}

		private void WalkDownTree_Click(object sender, RoutedEventArgs e)
		{
			TopLevelExceptionHandler(() =>
			{
				RunGremlinQuery(@"
g.V('1').emit().until(not(in('parent'))).repeat(in('parent')).hasLabel('LocationNode').valueMap(true).select('id', 'name').fold()
				");
			});
		}
		
		private void WalkUpFromFolder221_Click(object sender, RoutedEventArgs e)
		{
			TopLevelExceptionHandler(() =>
			{
				RunGremlinQuery(@"
g.V('15').emit().until(not(out('parent'))).repeat(out('parent')).valueMap(true).select('id', 'name').fold()
				");
			});
		}

		private void Disconnect_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				this.client?.Dispose();
				this.client = null;
			}
			catch (Exception ex)
			{
				Dump($"Exception:\r\n{ex}");
			}
		}

		private void RunQuery_Click(object sender, RoutedEventArgs e)
		{
			RunGremlinQuery(this.Query.Text);
		}

		private void RunGremlinQuery(string gremlinQuery)
		{
			//Clear();

			TopLevelExceptionHandler(() =>
			{
				var graph = GetGraph();

				var query = client.CreateGremlinQuery(graph, gremlinQuery);
				var resultCount = 0;

				while (query.HasMoreResults)
				{
					foreach (var result in query.ExecuteNextAsync().Result)
					{
						Dump($"{JsonConvert.SerializeObject(result, Formatting.Indented)}");
						resultCount++;
					}
				}

				Dump($"{resultCount} results:");
			});

			Dump(gremlinQuery);
		}

		// TODO - Generate ids instead.
		private int id = 1;

		private int CreateNode(int? parentId, string type, string name, bool booly, int numby)
		{
			var vertexId = this.id++;
			var edgeId = this.id++;
			var gremlinQuery = $"g.addV('{type}').property('id', '{vertexId}').property('name', '{name}').property('booly', {booly.ToString().ToLower()}).property('numby', {numby}).as('v1')" + Environment.NewLine;

			if (parentId != null)
			{
				gremlinQuery += $".addE('parent').to(g.V('{parentId}')).property('id', '{edgeId}').as('e1')" + Environment.NewLine + ".select('v1', 'e1')";
			}
			else
			{
				gremlinQuery += ".select('v1')";
			}

			RunGremlinQuery(gremlinQuery);

			return vertexId;
		}

		private void TopLevelExceptionHandler(Action doAction = null)
		{
			try
			{
				if (this.client == null)
				{
					Connect_Click(null, null);
				}

				if (this.client == null)
				{
					return;
				}

				if (doAction == null)
				{
					return;
				}

				doAction();
			}
			catch (Exception ex)
			{
				Dump($"Exception:\r\n{ex}");
			}
		}

		private void Dump(string message = "")//, bool clearFirst = false)
		{
			//if (clearFirst)
			//{
			//    this.Output.Text = message;
			//}
			//else
			//{
				this.Output.Text = message + Environment.NewLine + this.Output.Text;
			//}

			this.Output.ScrollToLine(0);
		}

		//private void Clear()
		//{
		//	this.Output.Text = string.Empty;
		//	this.Output.ScrollToLine(0);
		//}

		private void ShowResponse(IResourceResponseBase response)
		{
			using (var responseReader = new StreamReader(response.ResponseStream))
			{
				Dump($"{response.StatusCode} - {responseReader.ReadToEnd()}");
			}
		}
	}
}