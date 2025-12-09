using CodeChallenge.Api.Logic;
using CodeChallenge.Api.Models;
using CodeChallenge.Api.Repositories;
using FluentAssertions;
using Moq;

namespace CodeChallenge.Tests
{
    public class MessageLogicTests
    {
        private readonly Mock<IMessageRepository> _repoMock;
        private readonly MessageLogic _logic;
        private readonly Guid _orgId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        public MessageLogicTests()
        {
            _repoMock = new Mock<IMessageRepository>(MockBehavior.Strict);
            _logic = new MessageLogic(_repoMock.Object);
        }

        [Fact]
        public async Task CreateMessage_SuccessfulCreation_ReturnsCreatedResult()
        {
            // Arrange
            var createReq = new CreateMessageRequest
            {
                Title = "New Title",
                Content = new string('x', 20) 
            };

            _repoMock.Setup(r => r.GetByTitleAsync(_orgId, It.Is<string>(s => s.Equals("New Title", StringComparison.OrdinalIgnoreCase))))
                     .ReturnsAsync((Message?)null);

            _repoMock.Setup(r => r.CreateAsync(It.IsAny<Message>()))
                     .ReturnsAsync((Message m) =>
                     {
                         m.Id = Guid.NewGuid();
                         return m;
                     });

            // Act
            var result = await _logic.CreateMessageAsync(_orgId, createReq);

            // Assert
            result.Should().BeOfType<Created<Message>>();
            var created = result as Created<Message>;
            created!.Value.Should().NotBeNull();
            created.Value.Title.Should().Be("New Title");

            _repoMock.Verify(r => r.GetByTitleAsync(_orgId, It.IsAny<string>()), Times.Once);
            _repoMock.Verify(r => r.CreateAsync(It.IsAny<Message>()), Times.Once);
        }

        [Fact]
        public async Task CreateMessage_DuplicateTitle_ReturnsConflict()
        {
            // Arrange
            var createReq = new CreateMessageRequest
            {
                Title = "Existing",
                Content = new string('x', 20)
            };

            var existing = new Message
            {
                Id = Guid.NewGuid(),
                OrganizationId = _orgId,
                Title = "Existing",
                Content = "existing content",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _repoMock.Setup(r => r.GetByTitleAsync(_orgId, It.Is<string>(s => s.Equals("Existing", StringComparison.OrdinalIgnoreCase))))
                     .ReturnsAsync(existing);

            // Act
            var result = await _logic.CreateMessageAsync(_orgId, createReq);

            // Assert
            result.Should().BeOfType<Conflict>();
            var conflict = result as Conflict;
            conflict!.Message.Should().Contain("same title");

            _repoMock.Verify(r => r.GetByTitleAsync(_orgId, It.IsAny<string>()), Times.Once);
            _repoMock.Verify(r => r.CreateAsync(It.IsAny<Message>()), Times.Never);
        }

        [Fact]
        public async Task CreateMessage_ContentTooShort_ReturnsValidationError()
        {
            // Arrange
            var createReq = new CreateMessageRequest
            {
                Title = "Valid Title",
                Content = "short" 
            };

            // Act
            var result = await _logic.CreateMessageAsync(_orgId, createReq);

            // Assert
            result.Should().BeOfType<ValidationError>();
            var ve = result as ValidationError;
            ve!.Errors.Should().ContainKey("Content");
            ve.Errors["Content"].Should().ContainMatch("*at least 10*");

            _repoMock.Verify(r => r.GetByTitleAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
            _repoMock.Verify(r => r.CreateAsync(It.IsAny<Message>()), Times.Never);
        }

        [Fact]
        public async Task UpdateMessage_NonExistent_ReturnsNotFound()
        {
            // Arrange
            var updateReq = new UpdateMessageRequest
            {
                Title = "Any",
                Content = new string('x', 20),
                IsActive = true
            };

            var msgId = Guid.NewGuid();

            _repoMock.Setup(r => r.GetByIdAsync(_orgId, msgId))
                     .ReturnsAsync((Message?)null);

            // Act
            var result = await _logic.UpdateMessageAsync(_orgId, msgId, updateReq);

            // Assert
            result.Should().BeOfType<NotFound>();
            var nf = result as NotFound;
            nf!.Message.Should().Contain("not");

            _repoMock.Verify(r => r.GetByIdAsync(_orgId, msgId), Times.Once);
            _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Message>()), Times.Never);
        }

        [Fact]
        public async Task UpdateMessage_InactiveMessage_ReturnsConflict()
        {
            // Arrange
            var updateReq = new UpdateMessageRequest
            {
                Title = "Changed",
                Content = new string('x', 20),
                IsActive = false
            };

            var msgId = Guid.NewGuid();

            var existing = new Message
            {
                Id = msgId,
                OrganizationId = _orgId,
                Title = "Old",
                Content = "Some content that is long enough",
                IsActive = false, 
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            };

            _repoMock.Setup(r => r.GetByIdAsync(_orgId, msgId))
                     .ReturnsAsync(existing);

            // Act
            var result = await _logic.UpdateMessageAsync(_orgId, msgId, updateReq);

            // Assert
            result.Should().BeOfType<Conflict>();
            var cf = result as Conflict;
            cf!.Message.Should().Contain("active");

            _repoMock.Verify(r => r.GetByIdAsync(_orgId, msgId), Times.Once);
            _repoMock.Verify(r => r.UpdateAsync(It.IsAny<Message>()), Times.Never);
        }

        [Fact]
        public async Task DeleteMessage_NonExistent_ReturnsNotFound()
        {
            // Arrange
            var msgId = Guid.NewGuid();

            _repoMock.Setup(r => r.GetByIdAsync(_orgId, msgId))
                     .ReturnsAsync((Message?)null);

            // Act
            var result = await _logic.DeleteMessageAsync(_orgId, msgId);

            // Assert
            result.Should().BeOfType<NotFound>();
            var nf = result as NotFound;
            nf!.Message.Should().Contain("not");

            _repoMock.Verify(r => r.GetByIdAsync(_orgId, msgId), Times.Once);
            _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task DeleteMessage_Inactive_ReturnsValidationError()
        {
            var msgId = Guid.NewGuid();

            var existing = new Message
            {
                Id = msgId,
                OrganizationId = _orgId,
                Title = "Inactive Msg",
                Content = new string('x', 20),
                IsActive = false
            };

            _repoMock.Setup(r => r.GetByIdAsync(_orgId, msgId))
                     .ReturnsAsync(existing);

            var result = await _logic.DeleteMessageAsync(_orgId, msgId);

            result.Should().BeOfType<ValidationError>();
            var ve = result as ValidationError;
            ve!.Errors.Should().ContainKey("IsActive");

            _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }
    }
}
