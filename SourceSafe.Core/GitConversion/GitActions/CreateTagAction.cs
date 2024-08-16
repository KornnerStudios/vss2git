
namespace SourceSafe.GitConversion.GitActions
{
    /// <summary>
    /// Represents a git tag action.
    /// </summary>
    sealed class CreateTagAction(
        string mTag,
        string mTaggerName,
        string mTaggerEmail,
        string mMessage,
        DateTime mUtcTime
        ) : IGitAction
    {
        public bool Run(
            IO.SimpleLogger logger,
            IGitWrapper git,
            IGitStatistic stat)
        {
            logger.WriteLine("Creating git tag: {0}", mTag);

            git.Tag(mTag, mTaggerName, mTaggerEmail, mMessage, mUtcTime);

            stat.AddTag();

            return true;
        }
    };
}
