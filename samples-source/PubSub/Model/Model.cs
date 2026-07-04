using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Modeling;
using Microsoft.Modeling.Xrt;

namespace PubSub.Model
{

    #region Publisher

    [TypeBinding("PubSub.Implementation.Publisher")]
    public class Publisher
    {
        internal SetContainer<Subscriber> subscribers;

        [Rule(Action = "new this.Publisher()")]
        public Publisher()
        {
            subscribers = new SetContainer<Subscriber>();
        }
        
        [Rule(Action = "this.Publish(data)")]
        public void Publish(string data)
        {
            foreach (Subscriber sub in subscribers)
                sub.messages.Add(data);
        }
    }
    
    #endregion

    #region Subscriber

    [TypeBinding("PubSub.Implementation.Subscriber")]
    public class Subscriber
    {
        public SequenceContainer<string> messages;

        [Rule(Action = "new this.Subscriber(pub)")]
        public Subscriber(Publisher pub)
        {
            messages = new SequenceContainer<string>();
            pub.subscribers.Add(this);
        }

        [Rule(Action = "this.Received(data)")]
        public void Received(string data)
        {
            Condition.IsTrue(messages.Count > 0);
            Condition.IsTrue(messages[0] == data);
            messages.RemoveAt(0);
        }
               
    }
    #endregion
}
