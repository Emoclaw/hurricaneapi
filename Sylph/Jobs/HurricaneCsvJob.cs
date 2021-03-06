﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualBasic.FileIO;
using MongoDB.Bson;
using MongoDB.Driver;
using Quartz;
using Sylph.Models;

namespace Sylph.Jobs
{
    public class HurricaneCsvJob : IJob
    {
        private readonly IMongoCollection<Hurricane> _collection;

        public HurricaneCsvJob(HurricaneDatabaseSettings.IHurricaneDatabaseSettings settings)
        {
            MongoClient client = new MongoClient(settings.ConnectionString);
            IMongoDatabase database = client.GetDatabase(settings.DatabaseName);
            _collection = database.GetCollection<Hurricane>(settings.HurricaneCollectionName);
        }

        public Task Execute(IJobExecutionContext context)
        {
            List<string[]> rowList = new List<string[]>();

            List<Hurricane> hurricaneList = new List<Hurricane>();
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(
                    "https://www.ncei.noaa.gov/data/international-best-track-archive-for-climate-stewardship-ibtracs/v04r00/access/csv/ibtracs.ACTIVE.list.v04r00.csv",
                    "Data/active.csv");
            }

            using (WebClient client = new WebClient())
            {
                client.DownloadFile(
                    "https://www.ncei.noaa.gov/data/international-best-track-archive-for-climate-stewardship-ibtracs/v04r00/access/csv/ibtracs.last3years.list.v04r00.csv",
                    "Data/last3years.csv");
            }

            var parser = new TextFieldParser("Data/last3years.csv");
            parser.TextFieldType = FieldType.Delimited;
            parser.SetDelimiters(",");
            rowList.Clear();
            while (!parser.EndOfData)
            {
                string[] row = parser.ReadFields();
                rowList.Add(row);
            }

            List<DataPoints> dataPointList = new List<DataPoints>();
            int maxSpeed = 0;
            for (int i = 2; i < rowList.Count - 1; i++)
            {
                DateTime dateTime = DateTime.ParseExact(rowList[i][6], "yyyy-MM-dd HH:mm:ss", null);
                long unixTime = ((DateTimeOffset) dateTime).ToUnixTimeSeconds();
                dataPointList.Add(new DataPoints(Convert.ToDouble(rowList[i][8]),
                    Convert.ToDouble(rowList[i][9]),
                    unixTime,
                    Convert.ToInt32(rowList[i][161]),
                    rowList[i][23].Equals("") ? -1 : (Convert.ToInt32(rowList[i][23])),
                    rowList[i][25].Equals("") ? -6 : Convert.ToInt16(rowList[i][25]),
                    rowList[i][14].Equals("") ? -1 : Convert.ToInt32(rowList[i][14])));

                if (Convert.ToInt32(rowList[i][161]) > maxSpeed)
                {
                    maxSpeed = Convert.ToInt32(rowList[i][161]);
                }


                if (rowList[i][0] != rowList[i + 1][0] || (i + 3) == rowList.Count)
                {
                    hurricaneList.Add(new Hurricane(rowList[i][0],
                        new List<DataPoints>(dataPointList), rowList[i][5], false,
                        maxSpeed, dataPointList[0].time,
                        dataPointList[dataPointList.Count - 1].time));
                    dataPointList.Clear();
                    maxSpeed = 0;
                }
            }

            var activeParser = new TextFieldParser("Data/active.csv");
            activeParser.TextFieldType = FieldType.Delimited;
            activeParser.SetDelimiters(",");
            rowList.Clear();
            while (!activeParser.EndOfData)
            {
                string[] row = activeParser.ReadFields();
                rowList.Add(row);
            }

            int numOfActiveHurricanes = 0;
            List<string> activeHurricaneIds = new List<string>();
            for (int i = 2; i < rowList.Count - 1; i++)
            {
                if (rowList[i][0] != rowList[i + 1][0] || (i + 3) == rowList.Count)
                {
                    activeHurricaneIds.Add(rowList[i][0]);
                    numOfActiveHurricanes++;
                }
            }

            for (int i = 0; i < numOfActiveHurricanes; i++)
            {
                for (int j = hurricaneList.Count - 1; j >= 0; j--)
                {
                    if (activeHurricaneIds[i] != hurricaneList[j].id) continue;

                    hurricaneList[j].IsActive = true;
                    break;
                }
            }

            InsertManyOptions options = new InsertManyOptions();
            options.IsOrdered = false;
            var filter = Builders<Hurricane>.Filter;
            foreach (var h in hurricaneList)
            {
                _collection.ReplaceOneAsync(hurricane => hurricane.id.Equals(h.id), h,
                    new ReplaceOptions {IsUpsert = true});
            }

            return Task.CompletedTask;
        }
    }
}