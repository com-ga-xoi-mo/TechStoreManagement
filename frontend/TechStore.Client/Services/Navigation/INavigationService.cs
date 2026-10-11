using System;
using System.Collections.Generic;
using System.Linq;

namespace TechStore.Client.Services.Navigation;

/// <summary>
/// UI-independent navigation interface.
/// </summary>
public interface INavigationService
{
    RouteId CurrentRoute { get; }
    IReadOnlyList<RouteMetadata> GetAllowedMenuRoutes();
    bool NavigateTo(RouteId routeId);
    bool GoBack();
    void Reset();
    void SetFrameAdapter(IFrameAdapter frameAdapter);
    
    event EventHandler<RouteId>? RouteChanged;
}

/// <summary>
/// Decouples the navigation logic from the Uno Frame so it can be tested headless.
/// </summary>
public interface IFrameAdapter
{
    bool NavigateToView(RouteId routeId);
    bool GoBack();
    bool CanGoBack { get; }
}

public sealed class NavigationService : INavigationService
{
    private readonly Session.ICurrentSession _session;
    private IFrameAdapter? _frameAdapter;
    private readonly List<RouteId> _history = new();
    
    public RouteId CurrentRoute { get; private set; }

    public event EventHandler<RouteId>? RouteChanged;

    public NavigationService(Session.ICurrentSession session)
    {
        _session = session;
        // Start with Dashboard
        CurrentRoute = RouteId.Dashboard;
        _history.Add(CurrentRoute);
    }

    public void SetFrameAdapter(IFrameAdapter frameAdapter)
    {
        _frameAdapter = frameAdapter;
        // Initial navigation setup
        _frameAdapter.NavigateToView(CurrentRoute);
    }

    public IReadOnlyList<RouteMetadata> GetAllowedMenuRoutes()
    {
        return NavigationCatalog.GetMenuRoutes()
            .Where(r => r.RequiredCapabilityKey == null || _session.HasCapability(r.RequiredCapabilityKey))
            .ToList()
            .AsReadOnly();
    }

    public bool NavigateTo(RouteId routeId)
    {
        // No-op reselect
        if (CurrentRoute == routeId) return true;

        var route = NavigationCatalog.GetRoute(routeId);
        
        // Reject disallowed route
        if (route.RequiredCapabilityKey != null && !_session.HasCapability(route.RequiredCapabilityKey))
        {
            return false;
        }

        if (_frameAdapter != null)
        {
            var success = _frameAdapter.NavigateToView(routeId);
            if (!success) return false;
        }

        CurrentRoute = routeId;
        _history.Add(routeId);
        RouteChanged?.Invoke(this, CurrentRoute);
        return true;
    }

    public bool GoBack()
    {
        if (_history.Count <= 1) return false; // Root back rejection

        if (_frameAdapter != null)
        {
            if (!_frameAdapter.GoBack()) return false;
        }

        // Remove current from history
        _history.RemoveAt(_history.Count - 1);
        CurrentRoute = _history.Last();
        RouteChanged?.Invoke(this, CurrentRoute);
        
        return true;
    }

    public void Reset()
    {
        _frameAdapter = null;
        _history.Clear();
        CurrentRoute = RouteId.Dashboard;
        _history.Add(CurrentRoute);
        RouteChanged?.Invoke(this, CurrentRoute);
    }
}
