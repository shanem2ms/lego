#pragma once

namespace sam
{
    template <typename T, typename U> class index_map
    {
    public:
        typename std::map<T, U>::iterator insert(const std::pair<T, U> kv)
        {
            auto it = _map.insert(kv).first;
            _index.push_back(kv.first);
            return it;
        }

        typename std::map<T, U>::iterator begin()
        {
            return _map.begin();
        }
        typename std::map<T, U>::const_iterator begin() const
        {
            return _map.begin();
        }
        typename std::map<T, U>::iterator end()
        {
            return _map.end();
        }
        typename std::map<T, U>::const_iterator end() const
        {
            return _map.end();
        }
        typename std::map<T, U>::const_iterator find(const T& key) const
        {
            return _map.find(key);
        }
        typename std::map<T, U>::iterator find(const T& key)
        {
            return _map.find(key);
        }

        typename std::map<T, U>::iterator erase(typename std::map<T, U>::iterator it)
        {
            auto iterase = std::remove(_index.begin(), _index.end(), it->first);
            _index.erase(iterase, _index.end());
            return _map.erase(it);
        }

        U& operator [](const T& key)
        {
            return _map[key];
        }

        const U& operator [](const T& key) const
        {
            return _map[key];
        }

        void sort()
        {
            std::vector<std::pair<T, U>> items;
            for (const auto& pair : _map)
            {
                items.push_back(pair);
            }
            std::sort(items.begin(), items.end(), [](const std::pair<T, U>& lsh, std::pair<T, U>& rhs)
                {
                    return lsh.second < rhs.second;
                });
            _index.clear();
            for (const auto& pair : items)
            {
                _index.push_back(pair.first);
            }
        }
        size_t size() const { return _map.size(); }

        const std::vector<T>& keys() const
        {
            return _index;
        }
        std::vector<T>& keys()
        {
            return _index;
        }

    private:
        std::map<T, U>  _map;
        std::vector<T> _index;
    };
}