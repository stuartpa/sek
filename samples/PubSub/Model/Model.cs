using System.Collections.Generic;
using System.Linq;
using Sek.Modeling;

namespace PubSub.Model
{
    /// <summary>A subscriber owns an ordered queue of messages awaiting delivery.</summary>
    public sealed class Subscriber
    {
        public List<string> Messages { get; set; } = new List<string>();
    }

    /// <summary>A publisher fans a published message out to all its subscribers.</summary>
    public sealed class Publisher
    {
        public List<Subscriber> Subscribers { get; set; } = new List<Subscriber>();
    }

    /// <summary>
    /// The MS-CHAT-style publish/subscribe model, ported to SEK. Publisher and Subscriber
    /// objects are created during exploration; the rules that take one as a parameter draw
    /// their domain from the reachable objects (SEK's general reachable-object domain).
    /// </summary>
    public sealed class PubSubModel : ModelProgram
    {
        public List<Publisher> Publishers { get; set; } = new List<Publisher>();

        [Rule("Publisher")]
        public void NewPublisher()
        {
            Require(Publishers.Count < 1, "bound: a single publisher");
            Publishers.Add(new Publisher());
        }

        [Rule("Subscriber")]
        public void NewSubscriber(Publisher pub)
        {
            Require(pub.Subscribers.Count < 2, "bound: at most two subscribers");
            pub.Subscribers.Add(new Subscriber());
        }

        [Rule("Publisher.Publish")]
        public void Publish(Publisher pub, string data)
        {
            foreach (var s in pub.Subscribers)
            {
                s.Messages.Add(data);
            }
        }

        [Rule("Subscriber.Received")]
        public void Received(Subscriber sub, string data)
        {
            Require(sub.Messages.Count > 0, "no message waiting");
            Require(sub.Messages[0] == data, "must receive messages in order");
            sub.Messages.RemoveAt(0);
        }

        /// <summary>Accepting when there are no messages still awaiting delivery.</summary>
        [AcceptingCondition]
        public bool AllDelivered() =>
            Publishers.All(p => p.Subscribers.All(s => s.Messages.Count == 0));
    }
}
