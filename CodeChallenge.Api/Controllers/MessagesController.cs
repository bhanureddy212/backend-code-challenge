using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeChallenge.Api.Models;
using CodeChallenge.Api.Repositories;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CodeChallenge.Api.Controllers
{
    [ApiController]
    [Route("api/v1/organizations/{organizationId}/messages")]
    [Produces("application/json")]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageRepository _repository;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(IMessageRepository repository, ILogger<MessagesController> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Message>), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<ActionResult<IEnumerable<Message>>> GetAll(Guid organizationId)
        {
            if (organizationId == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid organizationId", Status = 400, Detail = "organizationId must be a non-empty GUID." });

            try
            {
                var messages = await _repository.GetAllByOrganizationAsync(organizationId);
                return Ok(messages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting messages for org {OrganizationId}", organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to get messages", Status = 500, Detail = ex.Message });
            }
        }

        [HttpGet("{id}", Name = nameof(GetById))]
        [ProducesResponseType(typeof(Message), 200)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<ActionResult<Message>> GetById(Guid organizationId, Guid id)
        {
            if (organizationId == Guid.Empty || id == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid id", Status = 400, Detail = "organizationId and id must be non-empty GUIDs." });

            try
            {
                var message = await _repository.GetByIdAsync(organizationId, id);
                if (message == null)
                    return NotFound();

                return Ok(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting message {MessageId} for org {OrganizationId}", id, organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to get message", Status = 500, Detail = ex.Message });
            }
        }

        [HttpPost]
        [ProducesResponseType(typeof(Message), 201)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<ActionResult<Message>> Create(Guid organizationId, [FromBody] CreateMessageRequest request)
        {
            if (organizationId == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid organizationId", Status = 400, Detail = "organizationId must be a non-empty GUID." });

            if (request == null)
                return BadRequest(new ProblemDetails { Title = "Invalid request", Status = 400, Detail = "Request body cannot be null." });

            // Basic validation
            if (string.IsNullOrWhiteSpace(request.Title))
                ModelState.AddModelError(nameof(request.Title), "Title is required.");

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                // Duplicate title check (repository expected to implement GetByTitleAsync)
                var existing = await _repository.GetByTitleAsync(organizationId, request.Title!.Trim());
                if (existing != null)
                {
                    return Conflict(new ProblemDetails
                    {
                        Title = "Duplicate title",
                        Status = 409,
                        Detail = "A message with the same title already exists for this organization."
                    });
                }

                var message = new Message
                {
                    OrganizationId = organizationId,
                    Title = request.Title.Trim(),
                    Content = request.Content ?? string.Empty,
                    IsActive = true
                };

                var created = await _repository.CreateAsync(message);

                return CreatedAtAction(nameof(GetById), new { organizationId = organizationId, id = created.Id }, created);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message for org {OrganizationId}", organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to create message", Status = 500, Detail = ex.Message });
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 409)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<ActionResult> Update(Guid organizationId, Guid id, [FromBody] UpdateMessageRequest request)
        {
            if (organizationId == Guid.Empty || id == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid id", Status = 400, Detail = "organizationId and id must be non-empty GUIDs." });

            if (request == null)
                return BadRequest(new ProblemDetails { Title = "Invalid request", Status = 400, Detail = "Request body cannot be null." });

            if (string.IsNullOrWhiteSpace(request.Title))
                ModelState.AddModelError(nameof(request.Title), "Title is required.");

            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            try
            {
                var existing = await _repository.GetByIdAsync(organizationId, id);
                if (existing == null)
                    return NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = "Message not found." });

                if (!string.Equals(existing.Title, request.Title!.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    var otherWithTitle = await _repository.GetByTitleAsync(organizationId, request.Title!.Trim());
                    if (otherWithTitle != null && otherWithTitle.Id != id)
                    {
                        return Conflict(new ProblemDetails
                        {
                            Title = "Duplicate title",
                            Status = 409,
                            Detail = "Another message with the same title already exists for this organization."
                        });
                    }
                }

                existing.Title = request.Title!.Trim();
                existing.Content = request.Content ?? string.Empty;
                existing.IsActive = request.IsActive;

                var updated = await _repository.UpdateAsync(existing);
                if (updated == null)
                {
                    return NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = "Message not found (could not update)." });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message {MessageId} for org {OrganizationId}", id, organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to update message", Status = 500, Detail = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(ProblemDetails), 400)]
        [ProducesResponseType(typeof(ProblemDetails), 404)]
        [ProducesResponseType(typeof(ProblemDetails), 500)]
        public async Task<ActionResult> Delete(Guid organizationId, Guid id)
        {
            if (organizationId == Guid.Empty || id == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid id", Status = 400, Detail = "organizationId and id must be non-empty GUIDs." });

            try
            {
                var deleted = await _repository.DeleteAsync(organizationId, id);
                if (!deleted)
                {
                    return NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = "Message not found." });
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {MessageId} for org {OrganizationId}", id, organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to delete message", Status = 500, Detail = ex.Message });
            }
        }
    }
}