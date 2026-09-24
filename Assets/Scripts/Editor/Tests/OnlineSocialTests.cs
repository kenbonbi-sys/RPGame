using System;
using System.IO;
using NUnit.Framework;

namespace RPG.EditorTools.Tests
{
    /// <summary>
    /// Playing together (online phase 4, Docs/KeHoach-Online.md): the rules of parties, what a line
    /// typed in the chat box means, and one character on one channel at a time.
    /// Plain EditMode tests: no scene, no Play Mode, no network.
    /// </summary>
    public class OnlineSocialTests
    {
        Parties parties;

        [SetUp]
        public void SetUp() => parties = new Parties();

        [Test]
        public void AnInvitationAnsweredYesMakesAParty()
        {
            Assert.IsNull(parties.Invite("Khang", "Mai", 0f), "the invitation goes out");
            Assert.AreEqual("Khang", parties.InvitedBy("mai", 1f), "names without case");
            var p = parties.Answer("Mai", "Khang", true, 2f, out string why);
            Assert.IsNull(why);
            Assert.NotNull(p);
            Assert.AreEqual("Khang", p.leader, "the one who invited leads");
            CollectionAssert.AreEqual(new[] { "Khang", "Mai" }, p.members);
            Assert.IsTrue(parties.SameParty("KHANG", "mai"));
            Assert.IsNull(parties.InvitedBy("Mai", 3f), "the invitation is used up");
        }

        [Test]
        public void ANoOrALateAnswerMakesNothing()
        {
            parties.Invite("Khang", "Mai", 0f);
            Assert.IsNull(parties.Answer("Mai", "Khang", false, 1f, out string why));
            Assert.IsNull(why, "a no is not an error");
            Assert.IsNull(parties.Of("Khang"));

            parties.Invite("Khang", "Mai", 0f);
            Assert.IsNull(parties.Answer("Mai", "Khang", true, Parties.InviteSeconds + 1f, out why));
            Assert.IsNotNull(why, "too late");
            Assert.IsNull(parties.Of("Mai"));
            Assert.IsNull(parties.Answer("Mai", "Tùng", true, 1f, out _), "nobody else invited them");
        }

        [Test]
        public void OnlyTheLeaderInvitesAndAPartyHasRoomForFive()
        {
            parties.Invite("A", "B", 0f);
            parties.Answer("B", "A", true, 0f, out _);
            Assert.IsNotNull(parties.Invite("B", "C", 0f), "a member who does not lead cannot invite");
            Assert.IsNotNull(parties.Invite("A", "A", 0f), "nor can anyone invite themselves");
            foreach (var n in new[] { "C", "D", "E" })
            {
                Assert.IsNull(parties.Invite("A", n, 0f));
                Assert.NotNull(parties.Answer(n, "A", true, 0f, out _));
            }
            Assert.AreEqual(Parties.MaxMembers, parties.Of("A").members.Count);
            Assert.IsNotNull(parties.Invite("A", "F", 0f), "full");
            Assert.IsNotNull(parties.Invite("X", "B", 0f), "someone in a party cannot be invited into another");
        }

        [Test]
        public void TheLeaderLeavingHandsThePartyOnAndTheLastOneEndsIt()
        {
            parties.Invite("A", "B", 0f);
            parties.Answer("B", "A", true, 0f, out _);
            parties.Invite("A", "C", 0f);
            parties.Answer("C", "A", true, 0f, out _);

            var p = parties.Leave("A");
            Assert.AreEqual("B", p.leader, "the next member leads");
            Assert.IsNull(parties.Of("A"));

            Assert.IsNotNull(parties.Kick("C", "B", out _), "only the leader sends people out");
            Assert.IsNull(parties.Kick("B", "C", out _));
            Assert.IsNull(parties.Of("C"));
            Assert.IsNull(parties.Of("B"), "a party of one is over");
            Assert.IsNull(parties.Leave("B"), "and has nothing to leave");
        }

        [Test]
        public void PartyMembersNearAKillShareIt()
        {
            var made = new System.Collections.Generic.List<UnityEngine.GameObject>();
            PlayerController Hero(string name, float x)
            {
                var go = new UnityEngine.GameObject(name);
                go.transform.position = new UnityEngine.Vector3(x, 0f);
                made.Add(go);
                return go.AddComponent<PlayerController>();
            }
            try
            {
                var hitter = Hero("hitter", 0f);
                var near = Hero("near", ServerPlayers.ShareRadius - 1f);
                var far = Hero("far", ServerPlayers.ShareRadius + 5f);
                var stranger = Hero("stranger", 1f);
                var party = new[] { hitter, near, far };
                var credited = new System.Collections.Generic.List<PlayerController> { hitter };
                ServerPlayers.ShareKill(credited, UnityEngine.Vector2.zero, h => System.Array.IndexOf(party, h) >= 0 ? party : null);
                CollectionAssert.AreEquivalent(new[] { hitter, near }, credited, "the member close to the kill shares it, the far one and strangers do not");

                credited = new System.Collections.Generic.List<PlayerController> { stranger };
                ServerPlayers.ShareKill(credited, UnityEngine.Vector2.zero, h => System.Array.IndexOf(party, h) >= 0 ? party : null);
                CollectionAssert.AreEqual(new[] { stranger }, credited, "a hero without a party keeps it alone");
            }
            finally
            {
                foreach (var go in made) UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ChatLinesAreCommandsOrTalk()
        {
            var c = ChatCommands.Parse("xin chào mọi người");
            Assert.AreEqual(ChatCommandKind.Say, c.kind);
            Assert.AreEqual("xin chào mọi người", c.arg);

            c = ChatCommands.Parse("/n đi đánh boss không?");
            Assert.AreEqual(ChatCommandKind.Party, c.kind);
            Assert.AreEqual("đi đánh boss không?", c.arg);

            c = ChatCommands.Parse("/w Thử Nghiệm chào bạn");
            Assert.AreEqual(ChatCommandKind.Whisper, c.kind);
            Assert.AreEqual("Thử Nghiệm chào bạn", c.arg, "the server finds where the name ends");

            Assert.AreEqual(ChatCommandKind.Invite, ChatCommands.Parse("/mời Mai").kind, "with Vietnamese marks");
            Assert.AreEqual("Mai", ChatCommands.Parse("/moi   Mai ").arg);
            Assert.AreEqual(ChatCommandKind.Accept, ChatCommands.Parse("/ĐồngÝ").kind);
            Assert.AreEqual(ChatCommandKind.Decline, ChatCommands.Parse("/tuchoi").kind);
            Assert.AreEqual(ChatCommandKind.Leave, ChatCommands.Parse("/rời").kind);
            Assert.AreEqual(ChatCommandKind.AddFriend, ChatCommands.Parse("/kếtbạn Tùng").kind);
            Assert.AreEqual(ChatCommandKind.Friends, ChatCommands.Parse("/banbe").kind);
            Assert.AreEqual(ChatCommandKind.Channel, ChatCommands.Parse("/kenh 2").kind);
            Assert.AreEqual("2", ChatCommands.Parse("/kenh 2").arg);
            Assert.AreEqual(ChatCommandKind.Help, ChatCommands.Parse("/?").kind);
            c = ChatCommands.Parse("/nhảy");
            Assert.AreEqual(ChatCommandKind.Unknown, c.kind);
            Assert.AreEqual("nhay", c.arg);
        }
    }

    /// <summary>Every channel is its own server on one data folder: a character plays on one of them at a time.</summary>
    public class OnlineChannelTests
    {
        string folder;
        ServerStore store;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "rtt_test_channels_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            store = new ServerStore(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void ACharacterPlaysOnOneChannelAtATime()
        {
            Assert.AreEqual(0, store.ChannelOf("Mai"), "nobody plays yet");
            var claim = store.TryLock("Mai", 2);
            Assert.NotNull(claim, "channel 2 has her");
            Assert.AreEqual(2, store.ChannelOf("mai"), "everyone on this folder sees where she plays");
            Assert.IsNull(store.TryLock("MAI", 1), "another channel waits for her");
            using (var other = store.TryLock("Tùng", 1)) Assert.NotNull(other, "other characters are free");

            claim.Dispose();
            Assert.AreEqual(0, store.ChannelOf("Mai"));
            using (var again = store.TryLock("Mai", 1))
            {
                Assert.NotNull(again, "after she left, channel 1 may have her");
                Assert.AreEqual(1, store.ChannelOf("Mai"));
            }
        }

        [Test]
        public void AClaimLeftBehindByAStoppedServerIsFree()
        {
            Directory.CreateDirectory(store.OnlineDir);
            File.WriteAllText(Path.Combine(store.OnlineDir, LoginCrypto.NameKey("Mai") + ".lock"), "3");
            Assert.AreEqual(0, store.ChannelOf("Mai"), "nobody holds it: she does not play");
            using (var claim = store.TryLock("Mai", 1)) Assert.NotNull(claim);
        }

        [Test]
        public void FriendsAreKeptWithTheAccount()
        {
            var a = store.CreateAccount("Khang", new byte[16], new byte[32], 1000);
            Assert.NotNull(a.friends);
            a.friends.Add("Mai");
            store.SaveAccount(a);
            CollectionAssert.AreEqual(new[] { "Mai" }, store.LoadAccount("khang").friends);
        }
    }
}
