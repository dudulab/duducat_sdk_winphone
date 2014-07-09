using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Text;

namespace DuduCat.Config
{
    internal class DBContext : DataContext
    {
        private static readonly string DBConnectionString = "Data Source=isostore:/duducat.sdf";

        public DBContext()
            : base(DBConnectionString)
        {
            if (!this.DatabaseExists())
            {
                CreateDatabase();
                SubmitChanges();
            }
            else
            {
                try
                {
                    Configs.FirstOrDefault();
                }
                catch
                {
                    DeleteDatabase();
                    CreateDatabase();
                    SubmitChanges();
                }
            }
        }

        public void Reset()
        {
            DeleteDatabase();
            CreateDatabase();
            SubmitChanges();
        }

        public Table<ConfigItem> Configs = null;
    }

    internal enum ItemStatus
    {
        /// <summary>
        /// Everything is fine.
        /// </summary>
        OK,

        /// <summary>
        /// Server returns KeyNotFound.
        /// </summary>
        KeyNotFound,

        /// <summary>
        /// Can't download the image.
        /// </summary>
        InvalidPath,
    }

    [Table]
    internal class ConfigItem : INotifyPropertyChanged, INotifyPropertyChanging
    {
        private string _id;

        private string _key;
        private string _value;
        private byte[] _blob;
        private ConfigType _type;
        private DateTime? _expireTime;
        private string _md5;
        private ItemStatus _status;
        private Binary _internalVersion;

        [Column(IsPrimaryKey = true)]
        public string ID
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    NotifyPropertyChanging("ID");
                    _id = value;
                    NotifyPropertyChanged("ID");
                }
            }
        }

        [Column]
        public string Key
        {
            get { return _key; }
            set
            {
                if (_key != value)
                {
                    NotifyPropertyChanging("Key");
                    _key = value;
                    NotifyPropertyChanged("Key");
                }
            }
        }

        [Column]
        public string Value
        {
            get { return _value; }
            set
            {
                if (_value != value)
                {
                    NotifyPropertyChanging("Value");
                    _value = value;
                    NotifyPropertyChanged("Value");
                }
            }
        }

        [Column(DbType = "image")]
        public byte[] Blob
        {
            get { return _blob; }
            set
            {
                if (_blob != value)
                {
                    NotifyPropertyChanging("Blob");
                    _blob = value;
                    NotifyPropertyChanged("Blob");
                }
            }
        }

        [Column]
        public ConfigType Type
        {
            get { return _type; }
            set
            {
                if (_type != value)
                {
                    NotifyPropertyChanging("Type");
                    _type = value;
                    NotifyPropertyChanged("Type");
                }
            }
        }

        [Column(CanBeNull = true)]
        public DateTime? ExpireTime
        {
            get { return _expireTime; }
            set
            {
                if (_expireTime != value)
                {
                    NotifyPropertyChanging("ExpireTime");
                    _expireTime = value;
                    NotifyPropertyChanged("ExpireTime");
                }
            }
        }

        [Column]
        public string MD5
        {
            get { return _md5; }
            set
            {
                if (_md5 != value)
                {
                    NotifyPropertyChanging("MD5");
                    _md5 = value;
                    NotifyPropertyChanged("MD5");
                }
            }
        }

        [Column]
        public ItemStatus Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    NotifyPropertyChanging("Status");
                    _status = value;
                    NotifyPropertyChanged("Status");
                }
            }
        }

        /// <summary>
        /// Version column aids update performance.
        /// </summary>
        /// 
        [Column(IsVersion = true)]
        private Binary InernalVersion
        {
            get { return _internalVersion; }
            set { _internalVersion = value; }
        }

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        // Used to notify that a property changed
        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        #endregion

        #region INotifyPropertyChanging Members

        public event PropertyChangingEventHandler PropertyChanging;

        // Used to notify that a property is about to change
        private void NotifyPropertyChanging(string propertyName)
        {
            if (PropertyChanging != null)
            {
                PropertyChanging(this, new PropertyChangingEventArgs(propertyName));
            }
        }

        #endregion
    }
}
