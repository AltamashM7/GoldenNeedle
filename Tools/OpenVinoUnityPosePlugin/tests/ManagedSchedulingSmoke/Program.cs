using GoldenNeedle.Core.Motion.Providers.MediaPipe;
using Unity.Collections;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static NativeArray<byte> Bytes(byte value)
{
    var data = new byte[16];
    Array.Fill(data, value);
    return new NativeArray<byte>(data);
}

static bool Publish(
    OpenVinoLatestFrameMailbox mailbox,
    NativeArray<byte> bytes,
    long timestamp,
    int convention,
    out bool replaced)
{
    return mailbox.Publish(
        bytes,
        width: 2,
        height: 2,
        strideBytes: 8,
        rotationDegrees: 0,
        timestampMillisec: timestamp,
        observedAtSeconds: timestamp / 1000d,
        publishedAtSeconds: timestamp / 1000d,
        publishedFrameCount: checked((int)timestamp),
        coordinateConventionVersion: convention,
        launchOriginHint: PreparedInferenceLaunchOrigin.ReadbackContinuation,
        out replaced);
}

var scheduler = new InferenceLaunchScheduler();
scheduler.MarkAccepted(1d);
Require(Math.Abs(scheduler.SecondsUntilIntervalElapsed(1.02d, 0.05d) - 0.03d) < 1e-9,
    "cadence remaining interval mismatch");
Require(scheduler.SecondsUntilIntervalElapsed(1.05d, 0.05d) == 0d,
    "cadence should be elapsed");

var mailbox = new OpenVinoLatestFrameMailbox();
Require(Publish(mailbox, Bytes(1), 10, 1, out var replaced), "first publish rejected");
Require(!replaced && mailbox.PendingCount == 1 && mailbox.BufferAllocationCount == 1,
    "first pending slot state invalid");
Require(mailbox.TryTake(1, out var active), "first active take failed");
Require(mailbox.HasActive && mailbox.PendingCount == 0, "active ownership state invalid");

Require(Publish(mailbox, Bytes(2), 20, 1, out replaced), "second publish rejected");
Require(!replaced && mailbox.PendingCount == 1 && mailbox.BufferAllocationCount == 2,
    "second slot state invalid");
Require(Publish(mailbox, Bytes(3), 30, 1, out replaced), "newest replacement rejected");
Require(replaced && mailbox.PendingCount == 1 && mailbox.BufferAllocationCount == 2,
    "pending replacement grew beyond two buffers");
Require(!mailbox.TryTake(1, out _), "mailbox permitted overlapping second inference");

mailbox.Complete(active);
Require(mailbox.TryTake(1, out var newest), "newest pending take failed");
Require(newest.TimestampMillisec == 30, "latest frame did not win");
Require(newest.SlotId != active.SlotId, "pending frame overwrote active slot");
mailbox.Complete(newest);

var staleMailbox = new OpenVinoLatestFrameMailbox();
Require(Publish(staleMailbox, Bytes(4), 40, 1, out _), "stale-case publish failed");
Require(!staleMailbox.TryTake(2, out _), "stale coordinate frame was consumed");
Require(staleMailbox.PendingCount == 0 && !staleMailbox.HasActive, "stale frame was not discarded");

var stoppedMailbox = new OpenVinoLatestFrameMailbox();
Require(Publish(stoppedMailbox, Bytes(5), 50, 1, out _), "stop-case publish failed");
stoppedMailbox.StopAcceptingAndClear();
Require(stoppedMailbox.PendingCount == 0, "stop did not clear pending frame");
Require(!Publish(stoppedMailbox, Bytes(6), 60, 1, out _), "stopped mailbox accepted a new frame");

Require(OpenVinoSchedulingPolicy.NextMonotonicTimestamp(100, 90) == 100,
    "clock-forward timestamp mismatch");
Require(OpenVinoSchedulingPolicy.NextMonotonicTimestamp(100, 100) == 101,
    "equal timestamp was not advanced");
Require(OpenVinoSchedulingPolicy.NextMonotonicTimestamp(99, 100) == 101,
    "clock regression was not made monotonic");

Console.WriteLine("OPENVINO_MANAGED_SCHEDULING_SMOKE=PASS");
Console.WriteLine($"buffers={mailbox.BufferAllocationCount}; pending={mailbox.PendingCount}; active={mailbox.HasActive}");
