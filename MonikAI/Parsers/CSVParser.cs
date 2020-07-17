// File: CSVParser.cs
// Created: 19.03.2018
// 
// See <summary> tags for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using MonikAI.Parsers.Models;

namespace MonikAI.Parsers
{
    internal class CSVParser : IParser
    {
        /// <summary>
        ///     Retrieves the csv data containing Monika's responses and triggers.
        /// </summary>
        /// <param name="csvFileName">Name of the csv file to be parsed.</param>
        /// <returns>A string containing the path to the csv data or null if there is no csv file to load.</returns>
        public string GetData(string csvFileName)
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MonikAI\\MonikAI " + MonikaiSettings.Default.Language + (MonikaiSettings.Default.Language == "English" ? " (Original)" : "") + " - " + csvFileName + ".csv");

            if (!File.Exists(path))
            {
                return null;
            }

            return path;
        }

        /// <summary>
        ///     Traverses a csv file delimited by semi-colons and populates a list of responses and their triggers.
        /// </summary>
        /// <param name="csvPath">The full path to the csv file.</param>
        /// <returns>A list of DokiResponses which contain a character's expressions and the triggers to those expressions</returns>
        public List<DokiResponse> ParseData(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath))
            {
                return new List<DokiResponse>();
            }

            using (var reader = new StreamReader(csvPath))
            {
                var characterResponses = new List<DokiResponse>();

                // Parse process responses
                while (!reader.EndOfStream)
                {
                    var res = new DokiResponse();

                    var row = reader.ReadLine();

                    if (row.StartsWith("#") || row.StartsWith("\"#") || string.IsNullOrWhiteSpace(row) ||
                        row.All(x => x == ',' || x == '\"'))
                    {
                        continue;
                    }

                    if (csvPath.EndsWith("Startup.csv"))
                    {
                        row = "," + row;
                    }

                    var columns = new List<StringBuilder>();

                    // Read columns seperated by ",", but also consider verbose entries in quotation marks
                    var currentIndex = 0;
                    var quotationCount = 0;
                    columns.Add(new StringBuilder());
                    foreach (var c in row)
                    {
                        if (quotationCount % 2 == 0 && c == ',')
                        {
                            quotationCount = 0;
                            currentIndex++;
                            columns.Add(new StringBuilder());
                            continue;
                        }

                        if (c == '"')
                        {
                            quotationCount++;

                            if (quotationCount % 2 == 1 && quotationCount > 1)
                            {
                                columns[currentIndex].Append(c);
                            }

                            continue;
                        }

                        columns[currentIndex].Append(c);
                    }

                    // Separate response triggers by comma in case there are multiple triggers to the current response
                    var responseTriggers = columns[1].ToString().Split(',');
                    foreach (var trigger in responseTriggers)
                    {
                        if (!string.IsNullOrWhiteSpace(trigger))
                        {
                            res.ResponseTriggers.Add(trigger.Trim());
                        }
                    }

                    // Get text/face pairs
                    for (var textCell = 2; textCell < columns.Count - 1; textCell += 2)
                    {
                        if (!string.IsNullOrWhiteSpace(columns[textCell].ToString()))
                        {
                            var resText = columns[textCell].ToString();
                            var responseLength = resText.Length;
                            var face = string.IsNullOrWhiteSpace(columns[textCell + 1].ToString())
                                ? "a"
                                : columns[textCell + 1].ToString(); // "a" face is default

                            // If text can fit in a single box then add it to the response chain, otherwise break it up.
                            while (responseLength > 0)
                            {
                                // The dialogue box capacity differs based on character width.
                                // From some testing it seems like a max of 90 works fine in a lot of cases but this isn't a perfect solution.
                                if (responseLength <= 90)
                                {
                                    res.ResponseChain.Add(new Expression(resText, face));
                                    break;
                                }

                                // Get next response segment and update remaining response
                                var responseSegment = resText.Substring(0, 90);
                                resText = resText.Substring(90).Trim();
                                responseLength = resText.Length;

                                res.ResponseChain.Add(new Expression(responseSegment.Trim(), face));
                            }
                        }
                    }

                    // Add response to list
                    characterResponses.Add(res);
                }

                return characterResponses;
            }
        }
    }
}