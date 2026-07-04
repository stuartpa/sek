using System.Collections.Generic;
using System.Linq;
using Sek.Modeling;

namespace SMB2.Model
{
    /// <summary>An open file within a tree; may or may not have been written.</summary>
    public sealed class SFile
    {
        public bool Written { get; set; }
        public bool Closed { get; set; }
    }

    /// <summary>A connected tree (share) holding open files.</summary>
    public sealed class Tree
    {
        public List<SFile> Files { get; set; } = new List<SFile>();
    }

    /// <summary>
    /// A simplified SMB2 protocol model, ported to SEK: session setup, tree connect, file
    /// create/write/close, and teardown. Trees and files are model-state objects; rules that
    /// act on them take a parameter whose domain is the reachable objects of that type.
    /// </summary>
    public sealed class Smb2Model : ModelProgram
    {
        public bool SessionUp { get; set; }
        public List<Tree> Trees { get; set; } = new List<Tree>();

        [Rule("SetupConnectionAndSession")]
        public void SetupConnectionAndSession()
        {
            Require(!SessionUp, "session already established");
            SessionUp = true;
        }

        [Rule("TreeConnect")]
        public void TreeConnect()
        {
            Require(SessionUp, "no session");
            Require(Trees.Count < 1, "bound: one tree");
            Trees.Add(new Tree());
        }

        [Rule("Create")]
        public void Create(Tree tree)
        {
            Require(tree.Files.Count < 2, "bound: two open files per tree");
            tree.Files.Add(new SFile());
        }

        [Rule("Write")]
        public void Write(SFile file)
        {
            Require(!file.Closed, "file is closed");
            Require(!file.Written, "already written");
            file.Written = true;
        }

        [Rule("Close")]
        public void Close(SFile file)
        {
            Require(!file.Closed, "already closed");
            file.Closed = true;
        }

        [Rule("TreeDisconnect")]
        public void TreeDisconnect(Tree tree)
        {
            Require(tree.Files.All(f => f.Closed), "close all files first");
            Trees.Remove(tree);
        }

        [Rule("LogOff")]
        public void LogOff()
        {
            Require(SessionUp, "no session");
            Require(Trees.Count == 0, "disconnect all trees first");
            SessionUp = false;
        }

        /// <summary>Accepting when the session is torn down (a completed protocol run).</summary>
        [AcceptingCondition]
        public bool SessionClosed() => !SessionUp && Trees.Count == 0;
    }
}
