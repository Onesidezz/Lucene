using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Directory = System.IO.Directory;

namespace LuceneFileSearch
{
    class Program
    {
        private static readonly LuceneVersion LuceneVersion = LuceneVersion.LUCENE_48;
        private static readonly string IndexPath = Path.Combine(Environment.CurrentDirectory, "index");
        private static IndexWriter indexWriter;
        private static StandardAnalyzer analyzer;

        static void Main(string[] args)
        {
            Console.WriteLine("=== Lucene.NET File Search Application ===\n");

            try
            {
                InitializeLucene();
                ShowMenu();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                CleanupLucene();
            }
        }

        static void InitializeLucene()
        {
            // Create index directory if it doesn't exist
            var indexDir = FSDirectory.Open(IndexPath);
            analyzer = new StandardAnalyzer(LuceneVersion);

            var config = new IndexWriterConfig(LuceneVersion, analyzer)
            {
                OpenMode = OpenMode.CREATE_OR_APPEND
            };

            indexWriter = new IndexWriter(indexDir, config);
            Console.WriteLine("Lucene.NET initialized successfully.");
        }

        static void ShowMenu()
        {
            while (true)
            {
                Console.WriteLine("\n--- Menu ---");
                Console.WriteLine("1. Index files from folder");
                Console.WriteLine("2. Search indexed files");
                Console.WriteLine("3. View index statistics");
                Console.WriteLine("4. Clear index");
                Console.WriteLine("5. Exit");
                Console.Write("Select an option (1-5): ");

                var choice = Console.ReadLine();
                Console.WriteLine();

                switch (choice)
                {
                    case "1":
                        IndexFiles();
                        break;
                    case "2":
                        SearchFiles();
                        break;
                    case "3":
                        ShowIndexStats();
                        break;
                    case "4":
                        ClearIndex();
                        break;
                    case "5":
                        Console.WriteLine("Goodbye!");
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }

        static void IndexFiles()
        {
            Console.Write("Enter the folder path or text file path: ");
            var path = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(path))
            {
                Console.WriteLine("Invalid input. Please try again.");
                return;
            }

            List<string> filesToIndex = new List<string>();

            if (File.Exists(path) && path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            {
                filesToIndex.Add(path);
            }
            else if (Directory.Exists(path))
            {
                filesToIndex.AddRange(Directory.GetFiles(path, "*.txt", SearchOption.AllDirectories));
            }
            else
            {
                Console.WriteLine("Path does not exist or is not a .txt file/folder.");
                return;
            }

            if (filesToIndex.Count == 0)
            {
                Console.WriteLine("No .txt files found.");
                return;
            }

            Console.WriteLine($"Found {filesToIndex.Count} text file(s). Indexing...");
            int indexed = 0;

            foreach (var filePath in filesToIndex)
            {
                try
                {
                    var content = File.ReadAllText(filePath);
                    var fileName = Path.GetFileName(filePath);

                    var doc = new Document();
                    doc.Add(new TextField("filename", fileName, Field.Store.YES));
                    doc.Add(new TextField("filepath", filePath, Field.Store.YES));
                    doc.Add(new TextField("content", content, Field.Store.YES));
                    doc.Add(new StringField("filetype", "txt", Field.Store.YES));

                    indexWriter.AddDocument(doc);
                    indexed++;
                    Console.WriteLine($"Indexed: {fileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error indexing {filePath}: {ex.Message}");
                }
            }

            indexWriter.Commit();
            Console.WriteLine($"\nSuccessfully indexed {indexed} file(s).");
        }

        //static void IndexFiles()
        //{
        //    Console.Write("Enter the folder path containing text files: ");
        //    var folderPath = Console.ReadLine();

        //    if (string.IsNullOrWhiteSpace(folderPath) || !System.IO.Directory.Exists(folderPath))
        //    {
        //        Console.WriteLine("Invalid folder path. Please try again.");
        //        return;
        //    }

        //    try
        //    {
        //        var textFiles = System.IO.Directory.GetFiles(folderPath, "*.txt", SearchOption.AllDirectories);

        //        if (textFiles.Length == 0)
        //        {
        //            Console.WriteLine("No .txt files found in the specified folder.");
        //            return;
        //        }

        //        Console.WriteLine($"Found {textFiles.Length} text file(s). Indexing...");
        //        int indexed = 0;

        //        foreach (var filePath in textFiles)
        //        {
        //            try
        //            {
        //                var content = File.ReadAllText(filePath);
        //                var fileName = Path.GetFileName(filePath);

        //                var doc = new Document();
        //                doc.Add(new TextField("filename", fileName, Field.Store.YES));
        //                doc.Add(new TextField("filepath", filePath, Field.Store.YES));
        //                doc.Add(new TextField("content", content, Field.Store.YES));
        //                doc.Add(new StringField("filetype", "txt", Field.Store.YES));

        //                indexWriter.AddDocument(doc);
        //                indexed++;
        //                Console.WriteLine($"Indexed: {fileName}");
        //            }
        //            catch (Exception ex)
        //            {
        //                Console.WriteLine($"Error indexing {filePath}: {ex.Message}");
        //            }
        //        }

        //        indexWriter.Commit();
        //        Console.WriteLine($"\nSuccessfully indexed {indexed} file(s).");
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error during indexing: {ex.Message}");
        //    }
        //}

        static void SearchFiles()
        {
            Console.Write("Enter search keywords: ");
            var query = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("Please enter a valid search query.");
                return;
            }

            try
            {
                using var reader = DirectoryReader.Open(indexWriter.Directory);
                var searcher = new IndexSearcher(reader);
                var parser = new MultiFieldQueryParser(LuceneVersion, new[] { "filename", "content" }, analyzer);

                var luceneQuery = parser.Parse(query);
                var hits = searcher.Search(luceneQuery, 20).ScoreDocs;

                if (hits.Length == 0)
                {
                    Console.WriteLine("No results found.");
                    return;
                }

                Console.WriteLine($"\nFound {hits.Length} result(s):\n");
                Console.WriteLine(new string('=', 80));

                for (int i = 0; i < hits.Length; i++)
                {
                    var doc = searcher.Doc(hits[i].Doc);
                    var fileName = doc.Get("filename");
                    var filePath = doc.Get("filepath");
                    var content = doc.Get("content");
                    var score = hits[i].Score;

                    Console.WriteLine($"Result {i + 1}:");
                    Console.WriteLine($"File: {fileName}");
                    Console.WriteLine($"Path: {filePath}");
                    Console.WriteLine($"Score: {score:F2}");

                    // Show all content snippets for this file
                    var snippets = GetAllContentSnippets(content, query, 250);
                    Console.WriteLine($"Found {snippets.Count} occurrence(s) in this file:");

                    for (int j = 0; j < snippets.Count; j++)
                    {
                        Console.WriteLine($"  Occurrence {j + 1}: {snippets[j]}");
                    }

                    Console.WriteLine(new string('-', 80));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during search: {ex.Message}");
            }
        }

        static List<string> GetAllContentSnippets(string content, string query, int maxLength)
        {
            var snippets = new List<string>();

            if (string.IsNullOrWhiteSpace(content))
            {
                snippets.Add("No content available.");
                return snippets;
            }

            var queryWords = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var contentLower = content.ToLower();

            var foundPositions = new List<int>();

            // Find all occurrences of query words
            foreach (var word in queryWords)
            {
                int index = 0;
                while ((index = contentLower.IndexOf(word, index)) != -1)
                {
                    foundPositions.Add(index);
                    index += word.Length;
                }
            }

            if (foundPositions.Count == 0)
            {
                var fallback = content.Length <= maxLength ? content : content.Substring(0, maxLength) + "...";
                snippets.Add(fallback);
                return snippets;
            }

            foundPositions.Sort();
            var filteredPositions = new List<int>();

            foreach (var pos in foundPositions)
            {
                bool tooClose = false;
                foreach (var existing in filteredPositions)
                {
                    if (Math.Abs(pos - existing) < maxLength / 2)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    filteredPositions.Add(pos);
                }
            }

            foreach (var position in filteredPositions)
            {
                // Find nearest sentence boundary before and after
                int start = position;
                //while (start > 0 && content[start] != '.' && content[start] != ',' && start > position - maxLength)
                //    start--;
                while (start > 0 && content[start] != '.' && start > position - maxLength)
                    start--;

                int end = position;
                //while (end < content.Length && content[end] != '.' && content[end] != ',' && end < position + maxLength)
                //    end++;
                while (end < content.Length && content[end] != '.' && end < position + maxLength)
                    end++;

                // Adjust bounds to avoid overshooting content length
                start = Math.Max(0, start);
                end = Math.Min(content.Length - 1, end);

                var snippet = content.Substring(start, end - start + 1).Trim();

                // Shorten if still too long
                if (snippet.Length > maxLength)
                {
                    snippet = snippet.Substring(0, maxLength) + "...";
                }

                // Add ellipses if start or end is trimmed
                if (start > 0) snippet = "..." + snippet;
                if (end < content.Length - 1) snippet += "...";

                // Highlight query terms
                foreach (var word in queryWords)
                {
                    var regex = new System.Text.RegularExpressions.Regex($@"\b{System.Text.RegularExpressions.Regex.Escape(word)}\b",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    snippet = regex.Replace(snippet, $"**{word.ToUpper()}**");
                }

                snippets.Add(snippet);
            }

            return snippets;
        }


        static string GetContentSnippet(string content, string query, int maxLength)
        {
            // Keep the old method for backward compatibility, but now it just returns the first snippet
            var snippets = GetAllContentSnippets(content, query, maxLength);
            return snippets.FirstOrDefault() ?? "No content available.";
        }
        static void ShowIndexStats()
        {
            try
            {
                // Read directly from the specified directory path
                var indexDirectory = FSDirectory.Open(@"C:\Users\ukhan2\Desktop\Index");
                using var reader = DirectoryReader.Open(indexDirectory);

                Console.WriteLine("=== Index Statistics ===");
                Console.WriteLine($"Total indexed documents: {reader.NumDocs}");
                Console.WriteLine($"Total deleted documents: {reader.NumDeletedDocs}");
                Console.WriteLine($"Index directory: C:\\Users\\ukhan2\\Desktop\\Index");

                if (reader.NumDocs > 0)
                {
                    Console.WriteLine("\nSample documents:");
                    var searcher = new IndexSearcher(reader);
                    var allDocs = searcher.Search(new MatchAllDocsQuery(), 170);
                    foreach (var hit in allDocs.ScoreDocs)
                    {
                        var doc = searcher.Doc(hit.Doc);
                        Console.WriteLine($"- {doc.Get("filename")} ({doc.Get("filepath")})");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving index statistics: {ex.Message}");
            }
        }
        //static void ShowIndexStats()
        //{
        //    try
        //    {
        //        indexWriter.Commit();
        //        using var reader = DirectoryReader.Open(indexWriter.Directory); // need to read from here C:\Users\ukhan2\Desktop\Index

        //        Console.WriteLine("=== Index Statistics ===");
        //        Console.WriteLine($"Total indexed documents: {reader.NumDocs}");
        //        Console.WriteLine($"Total deleted documents: {reader.NumDeletedDocs}");
        //        Console.WriteLine($"Index directory: {IndexPath}");

        //        if (reader.NumDocs > 0)
        //        {
        //            Console.WriteLine("\nSample documents:");
        //            var searcher = new IndexSearcher(reader);
        //            var allDocs = searcher.Search(new MatchAllDocsQuery(), 5);

        //            foreach (var hit in allDocs.ScoreDocs)
        //            {
        //                var doc = searcher.Doc(hit.Doc);
        //                Console.WriteLine($"- {doc.Get("filename")} ({doc.Get("filepath")})");
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error retrieving index statistics: {ex.Message}");
        //    }
        //}

        static void ClearIndex()
        {
            Console.Write("Are you sure you want to clear the entire index? (y/N): ");
            var confirmation = Console.ReadLine();

            if (confirmation?.ToLower() != "y")
            {
                Console.WriteLine("Index clearing cancelled.");
                return;
            }

            try
            {
                indexWriter.DeleteAll();
                indexWriter.Commit();
                Console.WriteLine("Index cleared successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing index: {ex.Message}");
            }
        }

        static void CleanupLucene()
        {
            try
            {
                indexWriter?.Dispose();
                analyzer?.Dispose();
                Console.WriteLine("Lucene.NET resources cleaned up.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
    }
}