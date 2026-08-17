using EduMaster.Domain.Common;
using EduMaster.Domain.Streams.ValueObjects;
using System;




namespace EduMaster.Domain.Streams
{
    public class Stream
    {
        public int StreamID { get; private set; }
        public StreamName StreamName { get; private set; }
        private bool _idSet = false;


        private Stream(StreamName streamName)
        {
            StreamName = streamName;
        }

        public static Stream Create(StreamName streamName)
        {
            return new Stream(streamName);
        }

        public void ChangeName(StreamName newName)
        {
            StreamName = newName;
        }

        internal void SetId(int id)  // internal أفضل من public
        {
            if (_idSet)  // منع إعادة التعيين
                throw new DomainException("لا يمكن تغيير الـ ID بعد تعيينه");

            if (id <= 0)
                throw new DomainException("الـ ID يجب أن يكون أكبر من صفر");

            StreamID = id;
            _idSet = true;
        }


    }
}
