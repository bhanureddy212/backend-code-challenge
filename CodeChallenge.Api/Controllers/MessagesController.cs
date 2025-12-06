using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CodeChallenge.Api.Logic;
using CodeChallenge.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace CodeChallenge.Api.Controllers
{
    [ApiController]
    [Route("api/v1/organizations/{organizationId}/messages")]
    [Produces("application/json")]
    public class MessagesController : ControllerBase
    {
        private readonly IMessageLogic _logic;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(IMessageLogic logic, ILogger<MessagesController> logger)
        {
            _logic = logic ?? throw new ArgumentNullException(nameof(logic));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Message>>> GetAll(Guid organizationId)
        {
            if (organizationId == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid organizationId", Status = 400, Detail = "organizationId must be a non-empty GUID." });

            var messages = await _logic.GetAllMessagesAsync(organizationId);
            return Ok(messages);
        }

        [HttpGet("{id}", Name = nameof(GetById))]
        public async Task<ActionResult<Message>> GetById(Guid organizationId, Guid id)
        {
            if (organizationId == Guid.Empty || id == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid id", Status = 400, Detail = "organizationId and id must be non-empty GUIDs." });

            var message = await _logic.GetMessageAsync(organizationId, id);
            if (message == null)
                return NotFound();

            return Ok(message);
        }

        [HttpPost]
        public async Task<ActionResult<Message>> Create(Guid organizationId, [FromBody] CreateMessageRequest request)
        {
            if (organizationId == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid organizationId", Status = 400, Detail = "organizationId must be a non-empty GUID." });

            if (request == null)
                return BadRequest(new ProblemDetails { Title = "Invalid request", Status = 400, Detail = "Request body cannot be null." });

            try
            {
                var result = await _logic.CreateMessageAsync(organizationId, request);
                return result switch
                {
                    Created<Message> c => CreatedAtAction(nameof(GetById), new { organizationId = organizationId, id = c.Value.Id }, c.Value),
                    ValidationError ve => ValidationProblem(new ValidationProblemDetails(ve.Errors)),
                    Conflict cf => Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = cf.Message }),
                    _ => StatusCode(500, new ProblemDetails { Title = "Unexpected result", Status = 500 })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating message for org {Org}", organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to create message", Status = 500, Detail = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> Update(Guid organizationId, Guid id, [FromBody] UpdateMessageRequest request)
        {
            if (organizationId == Guid.Empty || id == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid id", Status = 400, Detail = "organizationId and id must be non-empty GUIDs." });

            if (request == null)
                return BadRequest(new ProblemDetails { Title = "Invalid request", Status = 400, Detail = "Request body cannot be null." });

            try
            {
                var result = await _logic.UpdateMessageAsync(organizationId, id, request);
                return result switch
                {
                    Updated => NoContent(),
                    ValidationError ve => ValidationProblem(new ValidationProblemDetails(ve.Errors)),
                    NotFound nf => NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = nf.Message }),
                    Conflict cf => Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = cf.Message }),
                    _ => StatusCode(500, new ProblemDetails { Title = "Unexpected result", Status = 500 })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating message {Id} for org {Org}", id, organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to update message", Status = 500, Detail = ex.Message });
            }
        }

        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(Guid organizationId, Guid id)
        {
            if (organizationId == Guid.Empty || id == Guid.Empty)
                return BadRequest(new ProblemDetails { Title = "Invalid id", Status = 400, Detail = "organizationId and id must be non-empty GUIDs." });

            try
            {
                var result = await _logic.DeleteMessageAsync(organizationId, id);
                return result switch
                {
                    Deleted => NoContent(),
                    NotFound nf => NotFound(new ProblemDetails { Title = "Not found", Status = 404, Detail = nf.Message }),
                    Conflict cf => Conflict(new ProblemDetails { Title = "Conflict", Status = 409, Detail = cf.Message }),
                    _ => StatusCode(500, new ProblemDetails { Title = "Unexpected result", Status = 500 })
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message {Id} for org {Org}", id, organizationId);
                return StatusCode(500, new ProblemDetails { Title = "Failed to delete message", Status = 500, Detail = ex.Message });
            }
        }

        // Helper to convert validation dictionary to ModelStateDictionary-compatible structure
        private static IDictionary<string, string[]> ToModelState(Dictionary<string, string[]> errors)
        {
            return errors ?? new Dictionary<string, string[]>();
        }
    }
}
