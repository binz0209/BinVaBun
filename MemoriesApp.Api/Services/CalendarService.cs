using MongoDB.Driver;
using MemoriesApp.Api.Models;

namespace MemoriesApp.Api.Services;

public class CalendarService
{
    private readonly MongoDbService _mongoDb;

    public CalendarService(MongoDbService mongoDb)
    {
        _mongoDb = mongoDb;
    }

    public async Task<List<MarkedDate>> GetMarkedDatesAsync()
    {
        return await _mongoDb.MarkedDates.Find(_ => true).ToListAsync();
    }

    public async Task<MarkedDate?> GetMarkedDateAsync(DateTime date)
    {
        // Normalize date to start of day UTC for comparison
        var startOfDay = date.Date.ToUniversalTime();
        var endOfDay = startOfDay.AddDays(1).AddTicks(-1);
        
        var filter = Builders<MarkedDate>.Filter.And(
            Builders<MarkedDate>.Filter.Gte(d => d.Date, startOfDay),
            Builders<MarkedDate>.Filter.Lte(d => d.Date, endOfDay)
        );

        return await _mongoDb.MarkedDates.Find(filter).FirstOrDefaultAsync();
    }

    public async Task<MarkedDate> CreateMarkedDateAsync(DateTime date, string userId)
    {
        var markedDate = new MarkedDate
        {
            Date = date.Date.ToUniversalTime(), // Store at start of day
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        await _mongoDb.MarkedDates.InsertOneAsync(markedDate);
        return markedDate;
    }

    public async Task DeleteMarkedDateAsync(string id)
    {
        await _mongoDb.MarkedDates.DeleteOneAsync(d => d.Id == id);
    }

    public async Task<List<MarkedDate>> BatchUpdateDatesAsync(List<DateTime> addedDates, List<string> removedDateIds, string userId)
    {
        // Delete removed dates
        if (removedDateIds != null && removedDateIds.Count > 0)
        {
            var deleteFilter = Builders<MarkedDate>.Filter.In(d => d.Id, removedDateIds);
            await _mongoDb.MarkedDates.DeleteManyAsync(deleteFilter);
        }

        var newMarkedDates = new List<MarkedDate>();

        // Add new dates
        if (addedDates != null && addedDates.Count > 0)
        {
            foreach (var date in addedDates)
            {
                var markedDate = new MarkedDate
                {
                    Date = date.Date.ToUniversalTime(),
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };
                newMarkedDates.Add(markedDate);
            }

            if (newMarkedDates.Count > 0)
            {
                await _mongoDb.MarkedDates.InsertManyAsync(newMarkedDates);
            }
        }

        return newMarkedDates;
    }
}
