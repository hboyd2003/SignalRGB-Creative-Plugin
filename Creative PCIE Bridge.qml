Item {
    anchors.fill: parent
    Column {
        width: parent.width
        height: parent.height
        spacing: 10
        Column {
            width: 325
            height: 100

            Rectangle {
                width: parent.width
                height: parent.height
                color: "#a63221"
                radius: 5
                anchors.horizontalCenter: parent.horizontalCenter

                Row {   // Using a Row for horizontal arrangement
                    anchors.centerIn: parent
                    width: parent.width
                    height: parent.height

                    Image {
                        source: "https://raw.githubusercontent.com/SRGBmods/public/main/images/wlc/info-circle-fill.png"
                        anchors.verticalCenter: parent.verticalCenter
                    }

                    Column {  // A column to stack the texts vertically
                        y: 5
                        spacing: 10
                        width: parent.width
                        anchors.verticalCenter: parent.verticalCenter
                        //anchors.horizontalCenter: parent.horizontalCenter
                        Text {
                            color: theme.primarytextcolor
                            text: "<u><strong>Important</strong></u>"
                            horizontalAlignment: Text.AlignHCenter
                            width: parent.width
                            font.pixelSize: 17
                            font.family: "Roboto"
                            font.bold: false
                        }

                        Text {
                            leftPadding: 15
                            rightPadding: 15
                            color: theme.primarytextcolor
                            text: "This service <strong>REQUIRES</strong> its companion windows service
                                   \"Creative SignalRGB Bridge\" to be <strong>installed</strong> and <strong>running</strong>."

                            wrapMode: Text.WordWrap
                            width: parent.width
                            font.pixelSize: 13
                            font.family: "Roboto"
                            font.bold: false
                        }
                    }
                }
            }
        }
        Column {
            width: parent.width
            height: parent.height

            Repeater {
                model: service.controllers
                delegate: Item {
                                width: 325
                                height: 200
                                Rectangle {
                                    width: parent.width
                                    height: parent.height - 10
                                    color: "#2d2f31"
                                    radius: 5
                                }
                                Column {
                                    anchors.verticalCenter: parent.verticalCenter
                                    width: parent.width
                                    spacing: 5
                                    Image {
                                        id: logo
                                        width: parent.width - 20
                                        source: model.modelData.obj.logoURL
                                        fillMode: Image.PreserveAspectFit
                                        antialiasing: true
                                        mipmap: true
                                        anchors.horizontalCenter: parent.horizontalCenter
                                        clip: false
                                    }
                                    Text {
                                        color: "#FFFFFF"
                                        text: model.modelData.obj.name
                                        font.pixelSize: 20
                                        font.family: "Poppins"
                                        font.bold: true
                                        leftPadding: 15
                                    }
                                    Text {
                                        color: "#FFFFFF"
                                        text: "IP: " + model.modelData.obj.ip
                                        font.family: "Montserrat"
                                        font.pixelSize: 13
                                        leftPadding: 15
                                    }
                                    Text {
                                        color: "#FFFFFF"
                                        text: "Location ID: " + model.modelData.obj.id
                                        font.family: "Montserrat"
                                        font.pixelSize: 13
                                        leftPadding: 15
                                    }
                                }
                }
            }
        }
    }
}