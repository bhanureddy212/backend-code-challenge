using CodeChallenge.Api.Models;

namespace CodeChallenge.Api.Repositories;

/// <summary>
/// In-memory implementation of IMessageRepository
/// </summary>
public class InMemoryMessageRepository : IMessageRepository
{
    private readonly Dictionary<Guid, Message> _messages = new();
    private readonly object _lock = new();
    public InMemoryMessageRepository()
    {
        SeedSampleData();
    }

    public void SeedSampleData()
    {
        lock (_lock)
        {
            if (_messages.Any())
                return;

            var org1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var org2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

            var now = DateTime.UtcNow;

            var m1 = new Message
            {
                Id = Guid.NewGuid(),
                OrganizationId = org1,
                Title = "Welcome",
                Content = "Welcome to organization 1",
                IsActive = true,
                CreatedAt = now
            };

            var m2 = new Message
            {
                Id = Guid.NewGuid(),
                OrganizationId = org1,
                Title = "Reminder",
                Content = "Don't forget the all-hands",
                IsActive = true,
                CreatedAt = now.AddMinutes(-10)
            };

            var m3 = new Message
            {
                Id = Guid.NewGuid(),
                OrganizationId = org2,
                Title = "Org2 Notice",
                Content = "Notice for org 2",
                IsActive = false,
                CreatedAt = now.AddHours(-1)
            };

            _messages[m1.Id] = m1;
            _messages[m2.Id] = m2;
            _messages[m3.Id] = m3;
        }
    }

    public Task<Message?> GetByIdAsync(Guid organizationId, Guid id)
    {
        lock (_lock)
        {
            if (_messages.TryGetValue(id, out var message) && message.OrganizationId == organizationId)
            {
                return Task.FromResult<Message?>(message);
            }
            return Task.FromResult<Message?>(null);
        }
    }

    public Task<IEnumerable<Message>> GetAllByOrganizationAsync(Guid organizationId)
    {
        lock (_lock)
        {
            var messages = _messages.Values
                   .Where(m => m.OrganizationId == organizationId)
                  .OrderByDescending(m => m.CreatedAt)
                  .ToList();
            return Task.FromResult<IEnumerable<Message>>(messages);
        }
    }

    public Task<Message?> GetByTitleAsync(Guid organizationId, string title)
    {
        lock (_lock)
        {
            var message = _messages.Values
         .FirstOrDefault(m => m.OrganizationId == organizationId &&
             m.Title.Equals(title, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(message);
        }
    }

    public Task<Message> CreateAsync(Message message)
    {
        lock (_lock)
        {
            message.Id = Guid.NewGuid();
            message.CreatedAt = DateTime.UtcNow;
            _messages[message.Id] = message;
            return Task.FromResult(message);
        }
    }

    public Task<Message?> UpdateAsync(Message message)
    {
        lock (_lock)
        {
            if (_messages.ContainsKey(message.Id))
            {
                message.UpdatedAt = DateTime.UtcNow;
                _messages[message.Id] = message;
                return Task.FromResult<Message?>(message);
            }
            return Task.FromResult<Message?>(null);
        }
    }

    public Task<bool> DeleteAsync(Guid organizationId, Guid id)
    {
        lock (_lock)
        {
            if (_messages.TryGetValue(id, out var message) && message.OrganizationId == organizationId)
            {
                return Task.FromResult(_messages.Remove(id));
            }
            return Task.FromResult(false);
        }
    }
}
