
namespace USharpVideoQueue.Runtime
{
    public static class QueueArrayUtils
    {

        public static bool isFull(System.Object[] queue)
        {
            return queue[queue.Length - 1] != null;
        }

        public static bool isEmpty(System.Object[] queue)
        {
            return queue[0] == null;
        }

        public static System.Object first(System.Object[] queue)
        {
            return queue[0];
        }

        public static int firstEmpty(System.Object[] queue)
        {
            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i] == null) return i;
            }
            return -1;
        }

        public static bool enqueue(System.Object[] queue, System.Object element)
        {
            int index = firstEmpty(queue);
            if (index == -1) return false;
            queue[index] = element;
            return true;
        }

        public static void dequeue(System.Object[] queue)
        {
            remove(queue, 0);
        }

        public static void remove(System.Object[] queue, int index)
        {
            queue[index] = null;
            for (int i = index; i < queue.Length - 1; i++)
            {
                queue[i] = queue[i + 1];
            }
        }


    }
}