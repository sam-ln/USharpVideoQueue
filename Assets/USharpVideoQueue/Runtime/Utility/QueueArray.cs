
using System;
using VRC.SDKBase;

namespace USharpVideoQueue.Runtime.Utility
{
    public static class QueueArray
    {
        public static object EmptyReference(Array queue)
        {
            //Empty slots in UdonSynced arrays are not allowed to be null, thus we have
            // to determine an "Empty" object for different types. Generics are not yet exposed to Udon.
            //This will not run into nullpointers since all arrays must be initialized for Udon.
            //array.GetType().GetElementType() would be preferred, but it's not exposed to Udon.
            Type type = queue.GetValue(0).GetType();
            if (type == typeof(VRCUrl)) return VRCUrl.Empty;
            if (type == typeof(string)) return String.Empty;
            if (type == typeof(int)) return -1;
            return null;
        }

        public static bool IsFull(Array queue)
        {
            return !queue.GetValue(queue.Length - 1).Equals(EmptyReference(queue));
        }

        public static bool IsEmpty(Array queue)
        {
            return queue.GetValue(0).Equals(EmptyReference(queue));
        }

        public static object First(Array queue)
        {
            return queue.GetValue(0);
        }

        public static int FirstEmpty(Array queue)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                bool queueElementIsEmpty = queue.GetValue(i).Equals(EmptyReference(queue));
                if (queueElementIsEmpty) return i;
            }
            return -1;
        }

        public static int Count(Array queue)
        {
            int firstEmpty = FirstEmpty(queue);
            return firstEmpty != -1 ? firstEmpty : queue.Length;
        }

        public static bool Enqueue(Array queue, object element)
        {
            int index = FirstEmpty(queue);
            if (index == -1) return false;
            queue.SetValue(element, index);
            return true;
        }

        public static void Dequeue(Array queue)
        {
            Remove(queue, 0);
        }

        public static void Remove(Array queue, int index)
        {
            queue.SetValue(EmptyReference(queue), index);
            for (int i = index; i < queue.Length - 1; i++)
            {
                queue.SetValue(queue.GetValue(i + 1),i);
            }
            queue.SetValue(EmptyReference(queue), queue.Length-1);
        }

        public static void MoveUp(Array queue, int index)
        {
            if (index <= 0 || index >= queue.Length) return;
            object swap = queue.GetValue(index - 1);
            queue.SetValue(queue.GetValue(index), index-1);
            queue.SetValue(swap, index);
        }
        
        public static void MoveDown(Array queue, int index)
        {
            if (index < 0 || index >= Count(queue) - 1 ) return;
            object swap = queue.GetValue(index + 1);
            queue.SetValue(queue.GetValue(index), index+1);
            queue.SetValue(swap, index);
        }

        public static void ShiftBack(Array queue)
        {
            for (int i = queue.Length-1; i > 0; i--)
            {
                queue.SetValue(queue.GetValue(i-1), i);
            }
            queue.SetValue(EmptyReference(queue), 0);
        }

        public static void Clear(Array queue)
        {
            for (int i = 0; i < queue.Length-1; i++)
            {
                queue.SetValue(EmptyReference(queue), i);
            }
        }


    }
}