using System;
using System.Collections.Generic;
using TechStore.Client.Models.Identity;

namespace TechStore.Client.Services.Session;

/// <summary>
/// Status of the current user session
/// </summary>
public enum SessionStatus
{
    Unknown,
    Preview,
    Authenticated
}

/// <summary>
/// A snapshot of the current session state, avoiding direct bindings to active backend services
/// </summary>
public sealed record CurrentSessionSnapshot(
    SessionStatus Status,
    IReadOnlySet<string> Capabilities,
    string? DisplayName);

/// <summary>
/// Provides access to the current session state and acts as a coordinator for session transitions.
/// </summary>
public interface ICurrentSession
{
    CurrentSessionSnapshot Current { get; }
    bool HasCapability(string capabilityKey);
    
    void SetAuthenticated(SessionSnapshot identity);
    void SetPreview(string? overrideDisplayName = null);
    void ClearSession();

    event EventHandler<CurrentSessionSnapshot>? SessionChanged;
}

public sealed class CurrentSessionProvider : ICurrentSession
{
    public CurrentSessionSnapshot Current { get; private set; }

    public event EventHandler<CurrentSessionSnapshot>? SessionChanged;

    public CurrentSessionProvider()
    {
        // Start as Unknown until explicitly set to Preview or Authenticated
        Current = new CurrentSessionSnapshot(SessionStatus.Unknown, new HashSet<string>(), null);
    }

    public bool HasCapability(string capabilityKey)
    {
        return Current.Capabilities.Contains(capabilityKey);
    }

    public void SetAuthenticated(SessionSnapshot identity)
    {
        Current = new CurrentSessionSnapshot(
            SessionStatus.Authenticated, 
            identity.Capabilities, 
            identity.DisplayName);
        
        SessionChanged?.Invoke(this, Current);
    }

    public void SetPreview(string? overrideDisplayName = null)
    {
        var previewCapabilities = new HashSet<string>
        {
            "catalog:read",
            "inventory:read",
            "orders:read",
            "customers:read",
            "dev:preview"
        };

        if (CostAccessPreviewSettings.CurrentMode == CostAccessPreviewMode.Authorized)
        {
            previewCapabilities.Add("catalog:cost-read");
        }

        previewCapabilities.UnionWith(ProductEditCapabilityPreviewSettings.BuildPreviewCapabilities(
            ProductEditCapabilityPreviewSettings.EditMode,
            ProductEditCapabilityPreviewSettings.CostWriteMode));

        Current = new CurrentSessionSnapshot(SessionStatus.Preview, previewCapabilities, overrideDisplayName ?? "UI Preview Mode");
        SessionChanged?.Invoke(this, Current);
    }

    public void ClearSession()
    {
        Current = new CurrentSessionSnapshot(SessionStatus.Unknown, new HashSet<string>(), null);
        SessionChanged?.Invoke(this, Current);
    }
}
