using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeChallenge.Api.Models;
using CodeChallenge.Api.Repositories;

namespace CodeChallenge.Api.Logic
{
    /// <summary>
    /// Business logic for messages. Implements validation and business rules.
    /// </summary>
    public class MessageLogic : IMessageLogic
    {
        private readonly IMessageRepository _repository;

        public MessageLogic(IMessageRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<Result> CreateMessageAsync(Guid organizationId, CreateMessageRequest request)
        {
            var errors = ValidateRequest(request, isCreate: true);
            if (errors.Count > 0)
                return new ValidationError(errors);

            var exists = await _repository.GetByTitleAsync(organizationId, request.Title!.Trim());
            if (exists != null)
                return new Conflict("A message with the same title already exists for this organization.");

            var message = new Message
            {
                OrganizationId = organizationId,
                Title = request.Title!.Trim(),
                Content = request.Content ?? string.Empty,
                IsActive = true
            };

            var created = await _repository.CreateAsync(message);
            return new Created<Message>(created);
        }

        public async Task<Result> UpdateMessageAsync(Guid organizationId, Guid id, UpdateMessageRequest request)
        {
            var errors = ValidateRequest(request, isCreate: false);
            if (errors.Count > 0)
                return new ValidationError(errors);

            var existing = await _repository.GetByIdAsync(organizationId, id);
            if (existing == null)
                return new NotFound("Message not found.");

            if (!existing.IsActive)
                return new Conflict("Only active messages can be updated.");

            var trimmedTitle = (request.Title ?? string.Empty).Trim();
            if (!string.Equals(existing.Title?.Trim(), trimmedTitle, StringComparison.OrdinalIgnoreCase))
            {
                var other = await _repository.GetByTitleAsync(organizationId, trimmedTitle);
                if (other != null && other.Id != id)
                    return new Conflict("Another message with the same title already exists for this organization.");
            }

            existing.Title = trimmedTitle;
            existing.Content = request.Content ?? string.Empty;
            existing.IsActive = request.IsActive;

            var updated = await _repository.UpdateAsync(existing);
            if (updated == null)
                return new NotFound("Message not found (could not update).");

            return new Updated();
        }

        public async Task<Result> DeleteMessageAsync(Guid organizationId, Guid id)
        {
            var existing = await _repository.GetByIdAsync(organizationId, id);
            if (existing == null)
                return new NotFound("Message not found.");

            if (!existing.IsActive)
                return new Conflict("Only active messages can be deleted.");

            var deleted = await _repository.DeleteAsync(organizationId, id);
            if (!deleted)
                return new NotFound("Message not found (could not delete).");

            return new Deleted();
        }

        public Task<Message?> GetMessageAsync(Guid organizationId, Guid id)
            => _repository.GetByIdAsync(organizationId, id);

        public Task<IEnumerable<Message>> GetAllMessagesAsync(Guid organizationId)
            => _repository.GetAllByOrganizationAsync(organizationId);

        private static Dictionary<string, string[]> ValidateRequest(object request, bool isCreate)
        {
            var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            string GetStringProperty(string name)
            {
                var prop = request.GetType().GetProperty(name);
                return prop?.GetValue(request)?.ToString() ?? string.Empty;
            }

            string title = GetStringProperty("Title") ?? string.Empty;
            string content = GetStringProperty("Content") ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title))
                AddError("Title", "Title is required.");
            else
            {
                if (title.Trim().Length < 3)
                    AddError("Title", "Title must be at least 3 characters long.");
                if (title.Trim().Length > 200)
                    AddError("Title", "Title must be at most 200 characters long.");
            }

            if (string.IsNullOrWhiteSpace(content))
                AddError("Content", "Content is required.");
            else
            {
                if (content.Trim().Length < 10)
                    AddError("Content", "Content must be at least 10 characters long.");
                if (content.Trim().Length > 1000)
                    AddError("Content", "Content must be at most 1000 characters long.");
            }

            var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in errors)
                result[kvp.Key] = kvp.Value.ToArray();

            return result;

            void AddError(string key, string message)
            {
                if (!errors.TryGetValue(key, out var list))
                {
                    list = new List<string>();
                    errors[key] = list;
                }

                list.Add(message);
            }
        }
    }
}
