namespace AllocationReview.Api.Auth;

public static class Roles
{
    public const string Admin = "Admin";

    public const string Viewer = "Viewer";
}

public static class Policies
{
    /// <summary>Only Admins may use the run review endpoints (history and detail).</summary>
    public const string CanReviewRuns = nameof(CanReviewRuns);
}
