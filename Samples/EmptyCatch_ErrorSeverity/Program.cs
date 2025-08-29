try
{
    throw new Exception("boom");
}
catch
{
    // empty
}

// 2) Ignored when ignore_cancellation=true
try
{
    throw new OperationCanceledException();
}
catch (OperationCanceledException)
{
    // empty but ignored by config
}