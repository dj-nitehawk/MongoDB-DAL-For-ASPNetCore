﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using MongoDB.Entities.Tests.Models;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MongoDB.Entities.Tests;

[TestClass]
public class FileEntities
{
    const string dbName = "mongodb-entities-test-multi";

    [TestCategory("SkipWhenLiveUnitTesting")]
    //[TestMethod]
    public async Task uploading_data_from_http_stream()
    {
        await DB.InitAsync(dbName);

        var img = new Image { Height = 800, Width = 600, Name = "Test.Png" };
        await img.SaveAsync().ConfigureAwait(false);

        //https://placekitten.com/g/4000/4000 - 1097221
        //https://djnitehawk.com/test/test.bmp - 69455612
        using var stream = await new System.Net.Http.HttpClient().GetStreamAsync("https://djnitehawk.com/test/test.bmp").ConfigureAwait(false);
        await img.Data.UploadWithTimeoutAsync(stream, 30, 128).ConfigureAwait(false);

        var count = await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
                      .Where(c => c.FileID == img.ID)
                      .CountAsync();

        Assert.AreEqual(1097221, img.FileSize);
        Assert.AreEqual(img.ChunkCount, count);
    }

    [TestMethod]
    public async Task uploading_data_from_file_stream()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { Height = 800, Width = 600, Name = "Test.Png" };
        await img.SaveAsync().ConfigureAwait(false);

        using var stream = File.OpenRead("Models/test.jpg");
        await img.Data.UploadAsync(stream).ConfigureAwait(false);

        var count = await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
                      .Where(c => c.FileID == img.ID)
                      .CountAsync();

        Assert.AreEqual(2047524, img.FileSize);
        Assert.AreEqual(img.ChunkCount, count);
    }

    [TestMethod]
    public async Task uploading_with_wrong_hash()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { Height = 800, Width = 600, Name = "Test-bad-hash.png", MD5 = "wrong-hash" };
        await img.SaveAsync().ConfigureAwait(false);

        using var stream = File.OpenRead("Models/test.jpg");

        await Assert.ThrowsExceptionAsync<InvalidDataException>(async ()
            => await img.Data.UploadAsync(stream).ConfigureAwait(false));
    }

    [TestMethod]
    public async Task uploading_with_correct_hash()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { Height = 800, Width = 600, Name = "Test-correct-hash.png", MD5 = "cccfa116f0acf41a217cbefbe34cd599" };
        await img.SaveAsync().ConfigureAwait(false);

        using var stream = File.OpenRead("Models/test.jpg");
        await img.Data.UploadAsync(stream).ConfigureAwait(false);

        var count = await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
                      .Where(c => c.FileID == img.ID)
                      .CountAsync();

        Assert.AreEqual(2047524, img.FileSize);
        Assert.AreEqual(img.ChunkCount, count);
    }

    [TestMethod]
    public async Task file_smaller_than_chunk_size()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { Height = 100, Width = 100, Name = "Test-small.Png" };
        await img.SaveAsync().ConfigureAwait(false);

        using var stream = File.OpenRead("Models/test.jpg");
        await img.Data.UploadAsync(stream, 4096).ConfigureAwait(false);

        var count = await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
                      .Where(c => c.FileID == img.ID)
                      .CountAsync();

        Assert.AreEqual(2047524, img.FileSize);
        Assert.AreEqual(img.ChunkCount, count);
    }

    [TestMethod]
    public async Task deleting_entity_deletes_all_chunks()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { ID = Guid.NewGuid().ToString(), Height = 400, Width = 400, Name = "Test-Delete.Png" };
        await img.SaveAsync().ConfigureAwait(false);

        using var stream = File.Open("Models/test.jpg", FileMode.Open);
        await img.Data.UploadAsync(stream).ConfigureAwait(false);

        var countBefore =
            await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
              .Where(c => c.FileID == img.ID)
              .CountAsync();

        Assert.AreEqual(img.ChunkCount, countBefore);

        var deleteResult = await img.DeleteAsync();

        Assert.IsTrue(deleteResult.IsAcknowledged);
        Assert.AreEqual(1, deleteResult.DeletedCount);
        
        var countAfter =
            await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
              .Where(c => c.FileID == img.ID)
              .CountAsync();

        Assert.AreEqual(0, countAfter);
    }

    [TestMethod]
    public async Task deleting_only_chunks()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { Height = 400, Width = 400, Name = "Test-Delete.Png" };
        await img.SaveAsync().ConfigureAwait(false);

        using var stream = File.Open("Models/test.jpg", FileMode.Open);
        await img.Data.UploadAsync(stream).ConfigureAwait(false);

        var countBefore =
            await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
              .Where(c => c.FileID == img.ID)
              .CountAsync();

        Assert.AreEqual(img.ChunkCount, countBefore);

        //await img.DeleteAsync();

        await DB.File<Image>(img.ID).DeleteBinaryChunks();

        var countAfter =
            await DB.Database(dbName).GetCollection<FileChunk>(DB.CollectionName<FileChunk>()).AsQueryable()
              .Where(c => c.FileID == img.ID)
              .CountAsync();

        Assert.AreEqual(0, countAfter);
    }

    [TestMethod]
    public async Task downloading_file_chunks_works()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { Height = 500, Width = 500, Name = "Test-Download.Png" };
        await img.SaveAsync().ConfigureAwait(false);

        using (var inStream = File.OpenRead("Models/test.jpg"))
        {
            await img.Data.UploadAsync(inStream).ConfigureAwait(false);
        }

        using (var outStream = File.OpenWrite("Models/result.jpg"))
        {
            await img.Data.DownloadAsync(outStream, 3).ConfigureAwait(false);
        }

        using var md5 = MD5.Create();
        var oldHash = md5.ComputeHash(File.OpenRead("Models/test.jpg"));
        var newHash = md5.ComputeHash(File.OpenRead("Models/result.jpg"));

        Assert.IsTrue(oldHash.SequenceEqual(newHash));
    }

    [TestMethod]
    public async Task downloading_file_chunks_directly()
    {
        await InitTest.InitTestDatabase(dbName);
        DB.DatabaseFor<Image>(dbName);

        var img = new Image { Height = 500, Width = 500, Name = "Test-Download.Png" };
        await img.SaveAsync().ConfigureAwait(false);

        using (var inStream = File.OpenRead("Models/test.jpg"))
        {
            await img.Data.UploadAsync(inStream).ConfigureAwait(false);
        }

        using (var outStream = File.OpenWrite("Models/result-direct.jpg"))
        {
            await DB.File<Image>(img.ID).DownloadAsync(outStream).ConfigureAwait(false);
        }

        using var md5 = MD5.Create();
        var oldHash = md5.ComputeHash(File.OpenRead("Models/test.jpg"));
        var newHash = md5.ComputeHash(File.OpenRead("Models/result-direct.jpg"));

        Assert.IsTrue(oldHash.SequenceEqual(newHash));
    }

    [TestMethod]
    public void trying_to_download_when_no_chunks_present()
    {
        InitTest.InitTestDatabase(dbName).GetAwaiter().GetResult();

        Assert.ThrowsException<InvalidOperationException>(
            () =>
            {
                using var stream = File.OpenWrite("test.file");
                DB.File<Image>(ObjectId.GenerateNewId().ToString()!)
                  .DownloadAsync(stream).GetAwaiter().GetResult();
            });
    }
}