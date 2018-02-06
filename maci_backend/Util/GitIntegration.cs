using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Config;
using LibGit2Sharp;

namespace Backend.Util
{
    public class GitIntegration
    {

        public static bool CreateBackup(string repoFolder, string comment, GitRemoteOptions _gitRemoteOptions) {
            //var repoFolder = relativeFolder + "/" + name;
            using (var repo = new Repository(repoFolder)) {
                try
                {
                    var opt = new StageOptions();
                    Commands.Stage(repo, "*");
                    var sig = new Signature("maci-auto-committer", "auto-committer@maci-research.net", DateTimeOffset.Now);
                    repo.Commit(comment, sig, sig);

                    // Push to remote (e.g. Github) if the repo has a configured remote called "origin"
                    Remote remote = repo.Network.Remotes["origin"];
                    if (remote != null)
                    {
                        var options = new PushOptions();
                        options.CredentialsProvider = (_url, _user, _cred) =>
                            new UsernamePasswordCredentials
                            {
                                Username = _gitRemoteOptions.Username,
                                Password = _gitRemoteOptions.AccessToken
                            };
                        repo.Network.Push(remote, @"refs/heads/master", options);
                    }
                } catch(EmptyCommitException e)
                {
                    return false;
                }
            }
            return true;
        }


    }
}
