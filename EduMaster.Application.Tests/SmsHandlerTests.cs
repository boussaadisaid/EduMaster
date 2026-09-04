using EduMaster.Application.Abstractions;
using EduMaster.Application.Abstractions.Repositories;
using EduMaster.Application.Common;
using EduMaster.Application.Sms;
using EduMaster.Domain.Enums;
using EduMaster.Domain.Sms;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace EduMaster.Application.Tests;

public sealed class SmsHandlerTests
{
    [Fact]
    public async Task SaveSmsSettings_RejectsMissingDevice()
    {
        var store = new FakeSmsSettingsStore();
        var handler = new SaveSmsSettingsHandler(store);
        var result = await handler.ExecuteAsync(new SaveSmsSettingsRequest("api", ""));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.ErrorType);
    }


    [Fact]
    public async Task SendSmsBatch_PreservesPartialProviderAcceptanceInBatch()
    {
        var repo = new FakeSmsRepository();
        var provider = new FakeSmsProvider
        {
            Result = new SmsProviderSendResult(true, "batch-partial", 1, 1, null)
        };
        var store = new FakeSmsSettingsStore { Settings = new SmsGatewaySettings("api", "device-1") };
        var uow = new EduMaster.Application.Tests.Fakes.FakeUnitOfWork();
        var handler = new SendSmsBatchHandler(repo, provider, store, new EduMaster.Application.Tests.Fakes.FakeClock(), new EduMaster.Application.Tests.Fakes.FakeCurrentUserService(), uow, NullLogger<SendSmsBatchHandler>.Instance);

        var result = await handler.ExecuteAsync(new SendSmsRequest(
            SmsMessageCategory.Administrative, null,
            new[]
            {
                new SmsSendRecipient(null, null, "+213550123456", "أولى"),
                new SmsSendRecipient(null, null, "+213550123457", "ثانية")
            }));

        Assert.True(result.IsSuccess);
        Assert.Single(repo.Batches);
        Assert.Equal(1, repo.Batches[0].SubmittedCount);
        Assert.Equal(1, repo.Batches[0].FailedCount);
        Assert.Equal("batch-partial", repo.Batches[0].ProviderBatchId);
        Assert.All(repo.Messages, m => Assert.Equal(SmsMessageStatus.Pending, m.Status));
    }

    [Fact]
    public async Task SendSmsBatch_CreatesBatchAndMessages()
    {
        var repo = new FakeSmsRepository();
        var provider = new FakeSmsProvider();
        var store = new FakeSmsSettingsStore { Settings = new SmsGatewaySettings("api", "device-1") };
        var uow = new EduMaster.Application.Tests.Fakes.FakeUnitOfWork();
        var handler = new SendSmsBatchHandler(repo, provider, store, new EduMaster.Application.Tests.Fakes.FakeClock(), new EduMaster.Application.Tests.Fakes.FakeCurrentUserService(), uow, NullLogger<SendSmsBatchHandler>.Instance);

        var result = await handler.ExecuteAsync(new SendSmsRequest(
            SmsMessageCategory.Administrative, null,
            new[] { new SmsSendRecipient(null, null, "+213550123456", "مرحبا") }));

        Assert.True(result.IsSuccess);
        Assert.Single(repo.Messages);
        Assert.Equal("device-1", provider.DeviceId);
        Assert.Equal(1, result.Value!.RecipientCount);
        Assert.Equal(2, uow.CommittedCount);
        Assert.True(provider.Sent);
    }

    private sealed class FakeSmsProvider : ISmsProvider
    {
        public string? DeviceId { get; private set; }
        public bool Sent { get; private set; }
        public SmsProviderSendResult Result { get; set; } = new(true, "batch-1", 0, 0, null);
        public Task<IReadOnlyList<SmsProviderDevice>> GetDevicesAsync(string apiKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SmsProviderDevice>>(Array.Empty<SmsProviderDevice>());
        public Task<SmsProviderSendResult> SendBulkAsync(IReadOnlyList<SmsProviderMessage> messages, string deviceId, CancellationToken cancellationToken = default)
        { DeviceId = deviceId; Sent = true; return Task.FromResult(Result with { AcceptedCount = Result.AcceptedCount == 0 ? messages.Count : Result.AcceptedCount }); }
        public Task<SmsProviderBatchStatus> GetBatchAsync(string deviceId, string providerBatchId, CancellationToken cancellationToken = default)
            => Task.FromResult(new SmsProviderBatchStatus(providerBatchId, Array.Empty<SmsProviderDeliveryMessage>()));
    }

    private sealed class FakeSmsSettingsStore : ISmsSettingsStore
    {
        public SmsGatewaySettings Settings { get; set; } = new(null, null);
        public Task<SmsGatewaySettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Settings);
        public Task SaveAsync(SmsGatewaySettings settings, CancellationToken cancellationToken = default) { Settings = settings; return Task.CompletedTask; }
        public Task ClearAsync(CancellationToken cancellationToken = default) { Settings = new(null, null); return Task.CompletedTask; }
    }

    private sealed class FakeSmsRepository : ISmsRepository
    {
        public List<SmsBatch> Batches { get; } = new();
        public List<SmsMessage> Messages { get; } = new();
        public Task AddBatchAsync(SmsBatch batch, CancellationToken cancellationToken = default) { batch.SetIdForTests(Batches.Count + 1); Batches.Add(batch); return Task.CompletedTask; }
        public Task AddMessageAsync(SmsMessage message, CancellationToken cancellationToken = default) { message.SetIdForTests(Messages.Count + 1); Messages.Add(message); return Task.CompletedTask; }
        public Task AddDeliveryEventAsync(SmsDeliveryEvent deliveryEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<SmsBatch?> GetBatchByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Batches.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<SmsMessage>> GetMessagesByBatchIdAsync(int batchId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SmsMessage>>(Messages.Where(x => x.BatchId == batchId).ToList());
        public Task<IReadOnlyList<SmsHistoryItem>> GetHistoryAsync(DateTime? fromUtc, DateTime? toUtc, int? status, int? category, string? search, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SmsHistoryItem>>(Array.Empty<SmsHistoryItem>());
        public Task UpdateBatchAsync(SmsBatch batch, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateMessageAsync(SmsMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}



file static class SmsTestAccessors
{
    public static void SetIdForTests(this SmsBatch item, int id) => SetId(item, id);
    public static void SetIdForTests(this SmsMessage item, int id) => SetId(item, id);

    private static void SetId(SmsBatch item, int id)
    {
        var method = typeof(SmsBatch).GetMethod("SetId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(item, new object[] { id });
    }
    private static void SetId(SmsMessage item, int id)
    {
        var method = typeof(SmsMessage).GetMethod("SetId", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        method.Invoke(item, new object[] { id });
    }
}
