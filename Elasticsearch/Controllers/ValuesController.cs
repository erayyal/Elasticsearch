using System.Text.Json.Nodes;
using Dapper;
using Elasticsearch.Context;
using Elasticsearch.Models;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Elasticsearch.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ValuesController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public ValuesController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Create random data
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("action")]
    public async Task<IActionResult> CreateDate(CancellationToken cancellationToken)
    {
        IList<Travel> travels = new List<Travel>();
        var random = new Random();
        for (int i = 0; i < 5000; i++)
        {
            var title = new string(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            var words = new List<string>();
            for (int j = 0; j < 50; j++)
            {
                words.Add(new string(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 5)
                    .Select(s => s[random.Next(s.Length)]).ToArray()));
            }

            var description = string.Join(" ", words);
            var travel = new Travel
            {
                Title = title,
                Description = description
            };
            travels.Add(travel);
        }

        await _dbContext.Set<Travel>().AddRangeAsync(travels, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    /// <summary>
    /// Set datas to elasticsearch
    /// </summary>
    /// <returns></returns>
    [HttpGet("[action]")]
    public async Task<IActionResult> SyncToElastic()
    {
        var settings = new ConnectionConfiguration(new Uri("http://localhost:9200"));
        var client = new ElasticLowLevelClient(settings);
        List<Travel> travels = await _dbContext.Travels.ToListAsync();

        var tasks = new List<Task>();

        foreach (var travel in travels)
        {
            tasks.Add(client.IndexAsync<StringResponse>("travels", travel.Id.ToString(), PostData.Serializable(new
            {
                travel.Id,
                travel.Title,
                travel.Description
            })));
        }

        await Task.WhenAll(tasks);

        return Ok();
    }

    /// <summary>
    /// Get data list with elasticsearch
    /// </summary>
    /// <param name="title"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("[action]/{value}")]
    public async Task<IActionResult> GetDataListWithElasticSearch(string value, CancellationToken cancellationToken)
    {
        var settings = new ConnectionConfiguration(new Uri("http://localhost:9200"));
        var client = new ElasticLowLevelClient(settings);
        var response = await client.SearchAsync<StringResponse>("travels", PostData.Serializable(new
        {
            query = new
            {
                wildcard = new
                {
                    Description = new { value = $"*{value}*" }
                }
            }
        }));

        var results = JObject.Parse(response.Body);

        var hits = results["hits"]["hits"];

        List<Travel> travels = new();

        foreach (var hit in hits)
        {
            travels.Add(hit["_source"].ToObject<Travel>());
        }

        return Ok(travels);
    }
    
    /// <summary>
    /// Get data list with entity framework
    /// </summary>
    /// <param name="title"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("[action]/{value}")]
    public async Task<IActionResult> GetDataListWithEF(string value, CancellationToken cancellationToken)
    {
        IList<Travel> travels = await _dbContext.Set<Travel>()
            .Where(p => p.Description.Contains(value))
            .AsNoTracking()
            .ToListAsync();

        return Ok(travels);
    }

    /// <summary>
    /// Get data list with dapper
    /// </summary>
    /// <param name="value"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("[action]/{value}")]
    public async Task<IActionResult> GetDataWithDapper(string value, CancellationToken cancellationToken)
    {
        var travels = await _dbContext.Database.GetDbConnection().QueryAsync<Travel>(
            "SELECT * FROM Travels WHERE Description LIKE @value",
            new { value = $"%{value}%" });

        return Ok(travels);
    }
}