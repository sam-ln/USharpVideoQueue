
namespace USharpVideoQueue.Runtime
{
    public static class QueueArrayUtils
    {

        public static bool IsFull(System.Object[] queue)
        {
            return queue[queue.Length - 1] != null;
        }

        public static bool IsEmpty(System.Object[] queue)
        {
            return queue[0] == null;
        }

        public static System.Object First(System.Object[] queue)
        {
            return queue[0];
        }

        public static int FirstEmpty(System.Object[] queue)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] == null) return i;
            }
            return -1;
        }

        public static bool Enqueue(System.Object[] queue, System.Object element)
        {
            int index = FirstEmpty(queue);
            if (index == -1) return false;
            queue[index] = element;
            return true;
        }

        public static void Dequeue(System.Object[] queue)
        {
            Remove(queue, 0);
        }

        public static void Remove(System.Object[] queue, int index)
        {
            queue[index] = null;
            for (int i = index; i < queue.Length - 1; i++)
            {
                queue[i] = queue[i + 1];
            }
        }


    }
}