namespace sam
{
#define DLLSTTEST 1
    template<class T>
    class dbl_list
    {
    public:
        struct node
        {
            T data;
            node* next;
            node* prev;
            node(T &&val) : data(std::move(val)), next(nullptr), prev(nullptr) \
            {
            }
        };
        node* head, * tail;

        dbl_list() : head(nullptr), tail(nullptr) {}

        dbl_list(const dbl_list<T>& dll) = delete;
        dbl_list& operator=(dbl_list const&) = delete;

        void insert_front(node* node)
        {
            if (head == nullptr)
            {
                head = node;
                tail = node;
            }
            else
            {
                node->next = head;
                head = node;
                node->next->prev = node;
            }
        }

        void insert_back(node* node)
        {
            if (tail->next == nullptr)
            {
                tail->next = node;
                tail = node;
            }
        }


        void unlink(node* node)
        {
            if (head == tail) // one node, must be us
            { 
#if DLLSTTEST
                if (node != head || node != tail)
                    __debugbreak();
#endif
                head = tail = nullptr; 
                return;
            }
            if (node == head)
            { 
                head = node->next; 
                node->next->prev = nullptr;
                return;
            }
            if (node == tail)
            {
                tail = node->prev;
                node->prev->next = nullptr;
                return;
            }

            node->prev->next = node->next;
            node->next->prev = node->prev;
        }

    private:
      
    };
}