namespace DailyChallenges.Models
{
    public enum FriendRequestStatus
    {
        Pending = 0,
        Accepted = 1,
        /// <summary>
        /// The receiver declined. The row is kept (UpdatedAt = decline time) so the sender cannot re-request
        /// within the cooldown; afterwards a new request turns this row back into Pending.
        /// </summary>
        Declined = 2
    }
}
