using MembershipService.Domain;

namespace MembershipService.Application;

/// <summary>What happened when a member changed their membership: the root's answer, or that the account hasn't joined.</summary>
/// <typeparam name="TResult">The root's answer, e.g. <see cref="PauseResult"/>.</typeparam>
public sealed class MembershipCommandResult<TResult>
    where TResult : struct, Enum
{
    internal MembershipCommandResult(Membership? membership, TResult result)
    {
        Membership = membership;
        Result = result;
    }

    /// <summary>The membership as it is after the command, or null when the account hasn't joined.</summary>
    public Membership? Membership { get; }

    /// <summary>The root's answer. Has no meaning when <see cref="IsMember"/> is false.</summary>
    public TResult Result { get; }

    /// <summary>Whether the account has joined, with a membership.</summary>
    public bool IsMember => Membership is not null;
}

/// <summary>Creates a <see cref="MembershipCommandResult{TResult}"/>.</summary>
public static class MembershipCommandResult
{
    /// <summary>The root answered.</summary>
    /// <typeparam name="TResult">The root's answer type.</typeparam>
    /// <param name="membership">The membership after the command.</param>
    /// <param name="result">The root's answer.</param>
    /// <returns>The result.</returns>
    public static MembershipCommandResult<TResult> Answered<TResult>(Membership membership, TResult result)
        where TResult : struct, Enum
    {
        return new MembershipCommandResult<TResult>(membership, result);
    }

    /// <summary>The account hasn't joined.</summary>
    /// <typeparam name="TResult">The root's answer type.</typeparam>
    /// <returns>The result.</returns>
    public static MembershipCommandResult<TResult> NotAMember<TResult>()
        where TResult : struct, Enum
    {
        return new MembershipCommandResult<TResult>(membership: null, result: default);
    }
}
